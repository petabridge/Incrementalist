// -----------------------------------------------------------------------
// <copyright file="EmitDependencyGraphTask.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Git;
using Incrementalist.ProjectSystem;
using Incrementalist.ProjectSystem.Cmds;
using Incrementalist.Caching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd.Commands
{
    /// <summary>
    /// Analyzes changes and determines whether a full solution build or incremental build is required.
    /// </summary>
    public sealed class EmitDependencyGraphTask
    {
        private readonly CancellationTokenSource _cts;

        public EmitDependencyGraphTask(BuildSettings settings, MSBuildWorkspace workspace, ILogger logger)
        {
            Settings = settings;
            Workspace = workspace;
            Logger = logger;
            _cts = new CancellationTokenSource();
        }

        public BuildSettings Settings { get; }

        public MSBuildWorkspace Workspace { get; }

        public ILogger Logger { get; }

        public async Task<BuildAnalysisResult> Run()
        {
            // start the cancellation timer.
            _cts.CancelAfter(Settings.TimeoutDuration);

            var solution = await Workspace.OpenSolutionAsync(Settings.SolutionFile, null, _cts.Token);

            // Try to use cache if enabled
            if (!Settings.NoCache)
            {
                var cachePath = DependencyCacheIO.GetCachePath(Settings.WorkingDirectory);
                var existingCache = await DependencyCacheIO.LoadAsync(cachePath);

                if (await DependencyCacheHelper.IsCacheValidAsync(existingCache, solution, Settings.WorkingDirectory, Logger, _cts.Token))
                {
                    Logger.LogInformation("Using cached dependency information");
                    // TODO: Process using existingCache.Projects
                    // This will be implemented in the next step
                }
            }

            var getFilesCmd = new GatherAllFilesInSolutionCmd(Logger, _cts.Token, Settings.WorkingDirectory);
            var filterFilesCmd = new FilterAffectedProjectFilesCmd(Logger, _cts.Token, Settings.WorkingDirectory, Settings.TargetBranch);

            // Get all files and filter affected ones
            var allFiles = await getFilesCmd.Process(Task.FromResult(solution));
            var affectedFiles = await filterFilesCmd.Process(Task.FromResult(allFiles));

            // Early check: if no files are affected, return an incremental build with empty list
            if (!affectedFiles.Any())
            {
                Logger.LogInformation("No files were affected by the changes");
                return new IncrementalBuildResult(Array.Empty<string>());
            }

            // Check if any of the affected files require a solution-wide build
            var projectFiles = allFiles.Where(x => x.Value.FileType == FileType.Project)
                                     .Select(pair => new SlnFileWithPath(pair.Key, pair.Value))
                                     .ToList();
            var projectImports = ProjectImportsFinder.FindProjectImports(projectFiles);
            var detector = new SolutionWideChangeDetector(projectImports);

            if (detector.RequiresFullSolutionBuild(affectedFiles.Keys))
            {
                Logger.LogInformation("Solution-wide changes detected. Full solution build required");
                return new FullSolutionBuildResult(solution.FilePath);
            }

            // Get the list of affected projects
            var affectedProjects = affectedFiles.Where(x => x.Value.FileType == FileType.Project)
                                              .Select(x => x.Key)
                                              .ToList();

            // If all projects are affected, return a full solution build
            if (affectedProjects.Count == solution.Projects.Count())
            {
                Logger.LogInformation("All projects are affected. Full solution build required");
                return new FullSolutionBuildResult(solution.FilePath);
            }
            
            /* INCREMENTAL BUILDS */

            if (Settings.NoCache)
            {
                goto FullCompute;
            }
            
            // If we have a cache, we can try to use it to determine the incremental build
            // We also have to check to see that the cache is still valid
            var cache = await DependencyCacheIO.LoadAsync(DependencyCacheIO.GetCachePath(Settings.WorkingDirectory));
            if (cache is null)
            {
                Logger.LogInformation("No cache found. Full solution analysis required");
                goto FullCompute;
            }
            
            // Check if the cache is still valid
            var cacheIsValid = await DependencyCacheHelper.IsCacheValidAsync(cache, solution, Settings.WorkingDirectory, Logger, _cts.Token);
            if (!cacheIsValid)
            {
                Logger.LogInformation("Cache is invalid. Full solution analysis required");
                goto FullCompute;
            }
            else
            {
                // Happy path: cache is valid and we can use it to determine the incremental build
                Logger.LogInformation("Using cached dependency information");
                
                // Need to determine which projects need rebuilding based on the cache
                var cachedDependencyGraph = cache.GetProjectsToRebuild(affectedFiles);

                return ComputeFinalProjectGraph(cachedDependencyGraph);
            }

            FullCompute:
                // For incremental builds, compute the dependency graph
                var createDependencyGraph = new ComputeDependencyGraphCmd(Logger, _cts.Token, solution);
                var dependencyGraph = await createDependencyGraph.Process(Task.FromResult(affectedFiles));

                // Convert the dependency graph to a list of affected projects
                var finalGraph = ComputeFinalProjectGraph(dependencyGraph);

                try
                {
                    // Save the updated cache
                    if (!Settings.NoCache)
                    {
                        var newCache = await DependencyCacheHelper.CreateFromSolutionAsync(solution, Settings.WorkingDirectory);
                        await DependencyCacheIO.SaveAsync(DependencyCacheIO.GetCachePath(Settings.WorkingDirectory), newCache);
                    }
                }
                catch (Exception ex)
                {
                    // we can continue with our analysis even if there was an issue saving the cache
                    Logger.LogWarning(ex, "Failed to save cache");
                }
                
                return finalGraph;

                BuildAnalysisResult ComputeFinalProjectGraph(Dictionary<string, ICollection<string>> graph)
                {
                    var projectsToRebuild = graph.SelectMany(x => x.Value).Distinct().ToList();
                
                    // check to see if all the projects to rebuild == every project in the solution, in which case we need a full build
                    if (projectsToRebuild.Count == solution.Projects.Count())
                    {
                        Logger.LogInformation("All projects are affected. Full solution build required");
                        return new FullSolutionBuildResult(solution.FilePath);
                    }
                    
                    Logger.LogInformation("Incremental build possible. {ProjectCount} projects [{Projects}] need to be rebuilt", projectsToRebuild.Count, string.Join(", ", projectsToRebuild));
                    return new IncrementalBuildResult(projectsToRebuild);
                }
        }
    }
}
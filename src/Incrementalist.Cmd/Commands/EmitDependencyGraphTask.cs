// -----------------------------------------------------------------------
// <copyright file="EmitDependencyGraphTask.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.ProjectSystem;
using Incrementalist.ProjectSystem.Cmds;
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
        private readonly CancellationToken _ct;

        public EmitDependencyGraphTask(BuildSettings settings, MSBuildWorkspace workspace, ILogger logger, CancellationToken cancellation)
        {
            Settings = settings;
            Workspace = workspace;
            Logger = new WrappedLogger(logger, nameof(EmitDependencyGraphTask));
            _ct = cancellation;
        }

        public BuildSettings Settings { get; }

        public MSBuildWorkspace Workspace { get; }

        public ILogger Logger { get; }

        // Post-process the build result
        private BuildAnalysisResult FilterBuildResult(BuildAnalysisResult original, IReadOnlyList<SlnFileWithPath> allProjects)
        {
            var skipGlobs = Settings.SkipGlobs;
            var targetGlobs = Settings.TargetGlobs;

            if (targetGlobs.Count == 0 && skipGlobs.Count == 0)
                return original;

            var projectsToRebuild = original switch
            {
                FullSolutionBuildResult full => allProjects.Select(c => c.Path).ToList(),
                IncrementalBuildResult incremental => incremental.AffectedProjects,
                _ => []
            };

            // Need to process our globs

            // globbing is designed to work with relative paths
            var relativePaths = projectsToRebuild.Select(c =>
                Settings.WorkingDirectory.ComputeRelativePathToMe(c)).ToList();

            // we glob and then convert back into absolute paths
            var filteredProjects = GlobFilter.FilterProjects(relativePaths, skipGlobs, targetGlobs)
                .Select(c => c.ComputeAbsolutePath(Settings.WorkingDirectory)).ToList();

            if (filteredProjects.Count != projectsToRebuild.Count)
            {
                // had at least 1 hit on a filter
                Logger.LogInformation(
                    "Incrementalist selected {OriginalAffectedProjects} projects for rebuild, after filtering with globs: {FilteredAffectedProjects}",
                    projectsToRebuild.Count, filteredProjects.Count);

                return new IncrementalBuildResult(filteredProjects);
            }

            return original;
        }

        private async Task<(BuildAnalysisResult result, IReadOnlyList<SlnFileWithPath> allProjects)> RunInternal()
        {
            // start the cancellation timer.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
            linkedCts.CancelAfter(Settings.TimeoutDuration);
            
            var solutionFilePath = Path.Join(Settings.WorkingDirectory.Path, Settings.SolutionFile.Path);
            Logger.LogInformation("Opening solution {Solution}...", solutionFilePath);
            var progress = new Progress<ProjectLoadProgress>(x =>
            {
                Logger.LogDebug("{Operation} project {Project} in {ElapsedTime}", x.Operation, x.FilePath,
                    x.ElapsedTime);
            });

            var solution = await Workspace.OpenSolutionAsync(solutionFilePath, progress, linkedCts.Token);
            Logger.LogInformation("Solution opened successfully. Gathering solution files...");

            var getFilesCmd = new GatherAllFilesInSolutionCmd(Logger, linkedCts.Token, Settings.WorkingDirectory);
            var filterFilesCmd =
                new FilterAffectedProjectFilesCmd(Logger, linkedCts.Token, Settings.WorkingDirectory, Settings.TargetBranch);

            // Get all files and filter affected ones
            var allFiles = await getFilesCmd.Process(Task.FromResult(solution));
            Logger.LogInformation("Found {Count} files in solution", allFiles.Count);

            Logger.LogInformation("Analyzing Git changes...");
            var affectedFiles = await filterFilesCmd.Process(Task.FromResult(allFiles));
            Logger.LogInformation("Found {Count} affected files", affectedFiles.Count);

            // Early check: if no files are affected, return an incremental build with empty list
            if (affectedFiles.Count == 0)
            {
                Logger.LogInformation("No files were affected by the changes");
                return (new IncrementalBuildResult(Array.Empty<AbsolutePath>()), []);
            }

            // Log the breakdown of modified files by type
            var modifiedSourceFiles = affectedFiles.Count(x => x.Value.Any(f => f.FileType == FileType.Code));
            var modifiedProjectFiles = affectedFiles.Count(x => x.Value.Any(f => f.FileType == FileType.Project));
            var modifiedSolutionFiles = affectedFiles.Count(x => x.Value.Any(f => f.FileType == FileType.Solution));
            var modifiedScriptFiles = affectedFiles.Count(x => x.Value.Any(f => f.FileType == FileType.Script));
            var modifiedOtherFiles = affectedFiles.Count(x => x.Value.Any(f => f.FileType == FileType.Other));
            Logger.LogInformation(
                "Modified files breakdown: {SourceFiles} source files, {ProjectFiles} project files, {SolutionFiles} solution files, {ScriptFiles} script files, {OtherFiles} other files",
                modifiedSourceFiles, modifiedProjectFiles, modifiedSolutionFiles, modifiedScriptFiles,
                modifiedOtherFiles);

            // Check if any of the affected files require a solution-wide build
            Logger.LogInformation("Analyzing solution-wide impact...");
            var projectFiles = allFiles.Where(x => x.Value is [{ FileType: FileType.Project }])
                .Select(pair => new SlnFileWithPath(pair.Key, pair.Value[0]))
                .ToList();
            var projectImports = ProjectImportsFinder.FindProjectImports(projectFiles);
            var importDetector = new SolutionWideChangeDetector(projectImports);

            if (importDetector.RequiresFullSolutionBuild(affectedFiles.Keys))
            {
                Logger.LogInformation("Solution-wide changes detected. Full solution build required");
                return (new FullSolutionBuildResult(new AbsolutePath(solution.FilePath!)), projectFiles);
            }

            // Get the list of affected project files directly
            var directlyAffectedProjects = affectedFiles.Where(x => x.Value.Any(f => f.FileType == FileType.Project))
                .Select(x => x.Key)
                .ToList();

            // If all projects are affected, return a full solution build
            if (directlyAffectedProjects.Count == solution.Projects.Count())
            {
                Logger.LogInformation("All projects are affected. Full solution build required");
                return (new FullSolutionBuildResult(new AbsolutePath(solution.FilePath!)), projectFiles);
            }

            /* INCREMENTAL BUILDS */

            // For incremental builds, compute the dependency graph
            var createDependencyGraph = new ComputeDependencyGraphCmd(Logger, linkedCts.Token, solution);
            var dependencyGraph = await createDependencyGraph.Process(Task.FromResult(affectedFiles));

            // Convert the dependency graph to a list of affected projects
            var projectsToRebuild = dependencyGraph.SelectMany(x => x.Value).Distinct().ToList();

            // need to write a new cache
            return (ComputeResult(projectsToRebuild), projectFiles);

            BuildAnalysisResult ComputeResult(IReadOnlyList<AbsolutePath> projectFilePaths)
            {
                if (projectFilePaths.Count == 0)
                {
                    Logger.LogInformation("No projects need to be rebuilt");
                    return new IncrementalBuildResult(Array.Empty<AbsolutePath>());
                }

                if (projectFilePaths.Count == solution.Projects.Count())
                {
                    Logger.LogInformation("All projects are affected. Full solution build required");
                    return new FullSolutionBuildResult(new AbsolutePath(solution.FilePath!));
                }

                Logger.LogInformation(
                    "Incremental build possible. {RebuildCount} projects [{Projects}] need to be rebuilt",
                    projectFilePaths.Count, string.Join(", ", projectFilePaths));
                return new IncrementalBuildResult(projectFilePaths);
            }
        }

        public async Task<BuildAnalysisResult> Run()
        {
            var (buildResult, allProjects) = await RunInternal();

            // Post-process the build result
            var filteredResult = FilterBuildResult(buildResult, allProjects);
            return filteredResult;
        }
    }
}
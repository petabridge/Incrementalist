// -----------------------------------------------------------------------
// <copyright file="EmitDependencyGraphTask.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
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
        private readonly CancellationTokenSource _cts;

        public EmitDependencyGraphTask(BuildSettings settings, MSBuildWorkspace workspace, ILogger logger)
        {
            Settings = settings;
            Workspace = workspace;
            Logger = new WrappedLogger(logger, nameof(EmitDependencyGraphTask));
            _cts = new CancellationTokenSource();
        }

        public BuildSettings Settings { get; }

        public MSBuildWorkspace Workspace { get; }

        public ILogger Logger { get; }

        public async Task<BuildAnalysisResult> Run()
        {
            // start the cancellation timer.
            _cts.CancelAfter(Settings.TimeoutDuration);

            var solutionFilePath = Path.Join(Settings.WorkingDirectory.Path, Settings.SolutionFile.Path);
            Logger.LogInformation("Opening solution {Solution}...", solutionFilePath);
            var progress = new Progress<ProjectLoadProgress>(x =>
            {
                Logger.LogDebug("{Operation} project {Project} in {ElapsedTime}", x.Operation, x.FilePath,
                    x.ElapsedTime);
            });

            var solution = await Workspace.OpenSolutionAsync(solutionFilePath, progress, _cts.Token);
            Logger.LogInformation("Solution opened successfully. Gathering solution files...");

            var getFilesCmd = new GatherAllFilesInSolutionCmd(Logger, _cts.Token, Settings.WorkingDirectory);
            var filterFilesCmd =
                new FilterAffectedProjectFilesCmd(Logger, _cts.Token, Settings.WorkingDirectory, Settings.TargetBranch);

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
                return new IncrementalBuildResult(Array.Empty<AbsolutePath>());
            }

            // Log the breakdown of modified files by type
            var modifiedSourceFiles = affectedFiles.Count(x => x.Value.FileType == FileType.Code);
            var modifiedProjectFiles = affectedFiles.Count(x => x.Value.FileType == FileType.Project);
            var modifiedSolutionFiles = affectedFiles.Count(x => x.Value.FileType == FileType.Solution);
            var modifiedScriptFiles = affectedFiles.Count(x => x.Value.FileType == FileType.Script);
            var modifiedOtherFiles = affectedFiles.Count(x => x.Value.FileType == FileType.Other);
            Logger.LogInformation(
                "Modified files breakdown: {SourceFiles} source files, {ProjectFiles} project files, {SolutionFiles} solution files, {ScriptFiles} script files, {OtherFiles} other files",
                modifiedSourceFiles, modifiedProjectFiles, modifiedSolutionFiles, modifiedScriptFiles,
                modifiedOtherFiles);

            // Check if any of the affected files require a solution-wide build
            Logger.LogInformation("Analyzing solution-wide impact...");
            var projectFiles = allFiles.Where(x => x.Value.FileType == FileType.Project)
                .Select(pair => new SlnFileWithPath(pair.Key, pair.Value))
                .ToList();
            var projectImports = ProjectImportsFinder.FindProjectImports(projectFiles);
            var detector = new SolutionWideChangeDetector(projectImports);

            if (detector.RequiresFullSolutionBuild(affectedFiles.Keys))
            {
                Logger.LogInformation("Solution-wide changes detected. Full solution build required");
                return new FullSolutionBuildResult(new AbsolutePath(solution.FilePath!));
            }

            // Get the list of affected project files directly
            var directlyAffectedProjects = affectedFiles.Where(x => x.Value.FileType == FileType.Project)
                .Select(x => x.Key)
                .ToList();

            // If all projects are affected, return a full solution build
            if (directlyAffectedProjects.Count == solution.Projects.Count())
            {
                Logger.LogInformation("All projects are affected. Full solution build required");
                return new FullSolutionBuildResult(new AbsolutePath(solution.FilePath!));
            }

            /* INCREMENTAL BUILDS */

            // For incremental builds, compute the dependency graph
            var createDependencyGraph = new ComputeDependencyGraphCmd(Logger, _cts.Token, solution);
            var dependencyGraph = await createDependencyGraph.Process(Task.FromResult(affectedFiles));

            // Convert the dependency graph to a list of affected projects
            var projectsToRebuild = dependencyGraph.SelectMany(x => x.Value).Distinct().ToList();

            // need to write a new cache
            return ComputeResult(projectsToRebuild);

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
    }
}
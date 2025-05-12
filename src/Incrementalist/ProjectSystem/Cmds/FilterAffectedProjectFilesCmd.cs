// -----------------------------------------------------------------------
// <copyright file="FilterAffectedProjectFilesCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Git;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    ///     Filter the previously discovered <see cref="SlnFile" />s by whether
    ///     they were touched via the GitDiff.
    /// </summary>
    public sealed class
        FilterAffectedProjectFilesCmd : BuildCommandBase<Dictionary<AbsolutePath, SlnFile>,
        Dictionary<AbsolutePath, SlnFile>>
    {
        private readonly string _targetGitBranch;
        private readonly AbsolutePath _workingDirectory;

        public FilterAffectedProjectFilesCmd(ILogger logger, CancellationToken cancellationToken,
            AbsolutePath workingDirectory, string targetGitBranch)
            : base("FilterSlnFilesByGitDiff", logger, cancellationToken)
        {
            _workingDirectory = workingDirectory;
            _targetGitBranch = targetGitBranch;
        }

        protected override async Task<Dictionary<AbsolutePath, SlnFile>> ProcessImpl(
            Task<Dictionary<AbsolutePath, SlnFile>> previousTask)
        {
            var fileDictObj = await previousTask;

            var (repo, foundRepo) = GitRunner.FindRepository(_workingDirectory);
            if (!foundRepo || repo == null)
            {
                Logger.LogError("Unable to find Git repository located in {WorkingDirectory}. Shutting down.",
                    _workingDirectory);
                return new Dictionary<AbsolutePath, SlnFile>();
            }

            // validate the target branch
            if (!DiffHelper.HasBranch(repo, _targetGitBranch))
            {
                Logger.LogError("Current git repository doesn't have any branch named [{TargetBranch}]. Shutting down.",
                    _targetGitBranch);
                return new Dictionary<AbsolutePath, SlnFile>();
            }

            var affectedFiles = DiffHelper.ChangedFiles(repo, _targetGitBranch).ToList();

            var projectFiles = fileDictObj.Where(x => x.Value.FileType == FileType.Project).ToList();
            var projectFolders = projectFiles.Where(x => Path.GetDirectoryName(x.Key.Path) is not null)
                .ToLookup(x => new AbsolutePath(Path.GetDirectoryName(x.Key.Path)!), v => Tuple.Create(v.Key, v.Value));
            var projectImports =
                ProjectImportsFinder.FindProjectImports(projectFiles.Select(pair =>
                    new SlnFileWithPath(pair.Key, pair.Value)));

            // filter out any files that aren't affected by the diff
            var newDict = new Dictionary<AbsolutePath, SlnFile>();
            foreach (var file in affectedFiles)
            {
                Logger.LogDebug("Affected file: {FilePath}", file);
                // this file is in the solution
                if (fileDictObj.TryGetValue(file, out var value)) newDict[file] = value;
                else
                {
                    // special case - not all the affected files were in the solution.
                    // Check to see if these affected files are in the same folder as any of the projects
                    var directoryName = Path.GetDirectoryName(file.Path);
                    if (string.IsNullOrEmpty(directoryName))
                    {
                        continue;
                    }

                    // Need to see if this file is a solution-wide file
                    if (SolutionWideChangeDetector.IsSolutionWideFile(file))
                    {
                        Logger.LogInformation("Adding solution-wide file {File} to the set of affected files.", file);
                        newDict[file] = new SlnFile(FileType.Other, null);
                        continue;
                    }

                    if (TryFindSubFolder(projectFolders.Select(c => c.Key), new AbsolutePath(directoryName),
                            out var projectFolder))
                    {
                        var affectedProjects = projectFolders[projectFolder];
                        foreach (var (projectPath, project) in affectedProjects)
                        {
                            Logger.LogInformation(
                                "Adding project {0} to the set of affected files because non-code file {1}, " +
                                "found inside same directory [{2}], was modified.", projectPath, file, directoryName);
                            newDict[projectPath] = project;
                        }
                    }
                }

                // special case - if affected file was imported to some projects, need to mark importing project as affected
                if (projectImports.TryGetValue(file, value: out var import))
                {
                    // Mark all dependant as affected
                    foreach (var dependentProject in import.DependentProjects)
                    {
                        newDict[dependentProject.Path] = dependentProject.File;
                    }
                }
            }

            return newDict;
        }

        private static bool TryFindSubFolder(IEnumerable<AbsolutePath> testFolders, AbsolutePath targetFolder,
            [NotNullWhen(true)] out AbsolutePath? winningFolder)
        {
            winningFolder = null;
            foreach (var startingFolder in testFolders)
            foreach (var dir in Directory.EnumerateDirectories(startingFolder.Path))
            {
                if (Path.GetFullPath(dir).Equals(targetFolder.Path))
                {
                    winningFolder = startingFolder;
                    return true;
                }
            }

            return false;
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="FilterAffectedProjectFilesCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Git;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    ///     Filter the previously discovered <see cref="SlnFile" />s by whether or not
    ///     they were touched via the GitDiff.
    /// </summary>
    public sealed class
        FilterAffectedProjectFilesCmd : BuildCommandBase<Dictionary<string, SlnFile>, Dictionary<string, SlnFile>>
    {
        private readonly string _targetGitBranch;
        private readonly string _workingDirectory;

        public FilterAffectedProjectFilesCmd(ILogger logger, CancellationToken cancellationToken,
            string workingDirectory, string targetGitBranch)
            : base("FilterSlnFilesByGitDiff", logger, cancellationToken)
        {
            _workingDirectory = workingDirectory;
            _targetGitBranch = targetGitBranch;
        }

        protected override async Task<Dictionary<string, SlnFile>> ProcessImpl(
            Task<Dictionary<string, SlnFile>> previousTask)
        {
            var fileDictObj = await previousTask;

            var fileDict = (Dictionary<string, SlnFile>) fileDictObj;

            var repoResult = GitRunner.FindRepository(_workingDirectory);
            if (!repoResult.foundRepo)
            {
                Logger.LogError("Unable to find Git repository located in {0}. Shutting down.", _workingDirectory);
                return new Dictionary<string, SlnFile>();
            }

            // validate the target branch
            if (!DiffHelper.HasBranch(repoResult.repo, _targetGitBranch))
            {
                Logger.LogError("Current git repository doesn't have any branch named [{0}]. Shutting down.", _targetGitBranch);
                return new Dictionary<string, SlnFile>();
            }

            var repo = repoResult.repo;
            var affectedFiles = DiffHelper.ChangedFiles(repo, _targetGitBranch).ToList();

            var projectFiles = fileDict.Where(x => x.Value.FileType == FileType.Project).ToList();
            var projectFolders = projectFiles.ToLookup(x => Path.GetDirectoryName(x.Key), v => Tuple.Create(v.Key, v.Value));
            var projectImports = ProjectImportsFinder.FindProjectImports(projectFiles.Select(pair => new SlnFileWithPath(pair.Key, pair.Value)));

            // filter out any files that aren't affected by the diff
            var newDict = new Dictionary<string, SlnFile>();
            foreach (var file in affectedFiles)
            {
                Logger.LogDebug("Affected file: {0}", file);
                // this file is in the solution
                if (fileDict.ContainsKey(file)) newDict[file] = fileDict[file];
                else
                {
                    // special case - not all of the affected files were in the solution.
                    // Check to see if these affected files are in the same folder as any of the projects
                    var directoryName = Path.GetDirectoryName(file);

                    if (TryFindSubFolder(projectFolders.Select(c => c.Key), directoryName, out var projectFolder))
                    {
                        var affectedProjects = projectFolders[projectFolder];
                        foreach (var affectedProject in affectedProjects)
                        {
                            var project = affectedProject.Item2;
                            var projectPath = affectedProject.Item1;
                            Logger.LogInformation("Adding project {0} to the set of affected files because non-code file {1}, " +
                                                  "found inside same directory [{2}], was modified.", projectPath, file, directoryName);
                            newDict[projectPath] = project;
                        }
                    }
                }
                
                // special case - if affected file was imported to some projects, need to mark importing project as affected
                if (projectImports.ContainsKey(file))
                { 
                    // Mark all dependant as affected
                    projectImports[file].DependantProjects.ForEach(dependentProject => newDict[dependentProject.Path] = dependentProject.File);
                }
            }

            return newDict;
        }

        internal static bool TryFindSubFolder(IEnumerable<string> testFolders, string targetFolder, out string winningFolder)
        {
            winningFolder = null;
            foreach(var startingFolder in testFolders)
            foreach(var dir in Directory.EnumerateDirectories(startingFolder))
            {
                if (Path.GetFullPath(dir).Equals(targetFolder))
                {
                    winningFolder = startingFolder;
                    return true;
                }
            }
            return false;
        }
    }
}
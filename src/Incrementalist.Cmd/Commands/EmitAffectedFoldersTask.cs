// -----------------------------------------------------------------------
// <copyright file="EmitAffectedFoldersTask.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Git;
using Incrementalist.Git.Cmds;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd.Commands
{
    /// <summary>
    ///     List all of the folders affected by the current set of git commits
    /// </summary>
    public class EmitAffectedFoldersTask
    {
        private readonly CancellationTokenSource _cts;

        public EmitAffectedFoldersTask(BuildSettings settings, ILogger logger)
        {
            Settings = settings;
            Logger = logger;
            _cts = new CancellationTokenSource();
        }

        public BuildSettings Settings { get; }

        public ILogger Logger { get; }

        public async Task<IEnumerable<string>> Run()
        {
            // load the git repository
            var repoResult = GitRunner.FindRepository(Settings.WorkingDirectory);

            if (!repoResult.foundRepo)
            {
                Logger.LogError("Unable to find Git repository located in {0}. Shutting down.", Settings.WorkingDirectory);
                return new List<string>();
            }

            // validate the target branch
            if (!DiffHelper.HasBranch(repoResult.repo, Settings.TargetBranch))
            {
                Logger.LogError("Current git repository doesn't have any branch named [{0}]. Shutting down.", Settings.TargetBranch);
                return new List<string>();
            }

            // start the cancellation timer.
            _cts.CancelAfter(Settings.TimeoutDuration);
            var listAllFilesCmd = new ListAffectedFilesCmd(Logger, _cts.Token, Settings.TargetBranch);
            var filterAllFolders = new FilterAffectedFoldersCmd(Logger, _cts.Token);

            return await filterAllFolders.Process(listAllFilesCmd.Process(Task.FromResult(repoResult.repo)));
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="EmitAffectedFoldersTask.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
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
        private readonly CancellationToken _cts;

        public EmitAffectedFoldersTask(BuildSettings settings, ILogger logger, CancellationToken token)
        {
            Settings = settings;
            Logger = new WrappedLogger(logger, nameof(EmitAffectedFoldersTask));
            _cts = token;
        }

        public BuildSettings Settings { get; }

        public ILogger Logger { get; }

        public async Task<Dictionary<AbsolutePath, ICollection<AbsolutePath>>> Run()
        {
            // load the git repository
            var (repo, foundRepo) = GitRunner.FindRepository(Settings.WorkingDirectory);

            if (!foundRepo || repo == null)
            {
                Logger.LogError("Unable to find Git repository located in {WorkingDirectory}. Shutting down.",
                    Settings.WorkingDirectory);
                return new Dictionary<AbsolutePath, ICollection<AbsolutePath>>();
            }

            // validate the target branch
            if (!DiffHelper.HasBranch(repo, Settings.TargetBranch) && !DiffHelper.HasCommit(repo, Settings.TargetBranch))
            {
                Logger.LogError("Current git repository doesn't have any branch or commit [{BranchName}]. Shutting down.",
                    Settings.TargetBranch);
                return new Dictionary<AbsolutePath, ICollection<AbsolutePath>>();
            }

            // start the cancellation timer.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts);
            linkedCts.CancelAfter(Settings.TimeoutDuration);
            var listAllFilesCmd = new ListAffectedFilesCmd(Logger, linkedCts.Token, Settings.TargetBranch);
            var filterAllFolders = new FilterAffectedFoldersCmd(Logger, linkedCts.Token);

            return await filterAllFolders.Process(listAllFilesCmd.Process(Task.FromResult(repo)));
        }
    }
}
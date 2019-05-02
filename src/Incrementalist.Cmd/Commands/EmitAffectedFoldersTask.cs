﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Git;
using Incrementalist.Git.Cmds;
using Incrementalist.ProjectSystem.Cmds;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd.Commands
{
    /// <summary>
    /// List all of the folders affected by the current set of git commits
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

            // start the cancellation timer.
            _cts.CancelAfter(Settings.TimeoutDuration);
            var listAllFilesCmd = new ListAffectedFilesCmd(Logger, _cts.Token, Settings.TargetBranch);
            var filterAllFolders = new FilterAffectedFoldersCmd(Logger, _cts.Token);

            return await filterAllFolders.Process(listAllFilesCmd.Process(Task.FromResult(repoResult.repo)));
        }
    }
}

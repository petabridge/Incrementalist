using System.Collections.Generic;
using Incrementalist.ProjectSystem.Cmds;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Incrementalist.Cmd.Commands
{
    /// <summary>
    /// Used to emit an entire dependency graph based on which files in
    /// a solution were affected.
    /// </summary>
    public sealed class EmitDependencyGraphTask
    {
        public EmitDependencyGraphTask(BuildSettings settings, MSBuildWorkspace workspace, ILogger logger)
        {
            Settings = settings;
            _workspace = workspace;
            Logger = logger;
            _cts = new CancellationTokenSource();
        }

        private readonly MSBuildWorkspace _workspace;
        private readonly CancellationTokenSource _cts;

        public BuildSettings Settings { get; }

        public ILogger Logger { get; }

        public async Task<IEnumerable<string>> Run()
        {
            // start the cancellation timer.
            _cts.CancelAfter(Settings.TimeoutDuration);
            var loadSln = new LoadSolutionCmd(Logger, _workspace, _cts.Token);
            var slnFile = await loadSln.Process(Task.FromResult(Settings.SolutionFile));


            var getFilesCmd = new GatherAllFilesInSolutionCmd(Logger, _cts.Token, Settings.WorkingDirectory);
            var filterFilesCmd = new FilterAffectedProjectFilesCmd(Logger, _cts.Token, Settings.WorkingDirectory, Settings.TargetBranch);
            var createDependencyGraph = new ComputeDependencyGraphCmd(Logger, _cts.Token, slnFile);
            var affectedFiles = await createDependencyGraph.Process(filterFilesCmd.Process(getFilesCmd.Process(Task.FromResult(slnFile))));
            return affectedFiles;
        }
    }
}

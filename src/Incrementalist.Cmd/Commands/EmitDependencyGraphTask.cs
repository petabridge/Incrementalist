using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Incrementalist.ProjectSystem.Cmds;
using Microsoft.CodeAnalysis.MSBuild;

namespace Incrementalist.Cmd.Commands
{
    /// <summary>
    /// Used to emit an entire dependency graph based on which files in
    /// a solution were affected.
    /// </summary>
    public sealed class EmitDependencyGraphTask
    {
        public EmitDependencyGraphTask(BuildSettings settings, MSBuildWorkspace workspace, ILO)
        {
            Settings = settings;
            _workspace = workspace;
        }

        private readonly MSBuildWorkspace _workspace;

        public BuildSettings Settings { get; }

        

        public async Task<string> Run()
        {
            var solution = new GatherAllFilesInSolutionCmd()
        }
    }
}

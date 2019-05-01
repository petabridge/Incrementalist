using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd
{
    /// <summary>
    /// Load a VS <see cref="Solution"/> from file and analyze its contents.
    /// </summary>
    public sealed class LoadSolutionCmd : BuildCommandBase<string, Solution>
    {
        private readonly MSBuildWorkspace _workspace;
        private readonly Progress<ProjectLoadProgress> _progress;

        public LoadSolutionCmd(ILogger logger, MSBuildWorkspace workspace, CancellationToken token) : base("LoadMsBuildWorkspace", logger, token)
        {
            _workspace = workspace;
            _progress = new Progress<ProjectLoadProgress>(p =>
            {
                Logger.LogInformation("[{0}][{1}] - {2} [{3}]", p.ElapsedTime, p.Operation, p.FilePath, p.TargetFramework);
            });
        }

        protected override async Task<Solution> ProcessImpl(Task<string> previousTask)
        {
            var slnObject = await previousTask;
            Contract.Assert(slnObject is string fileName && !string.IsNullOrEmpty(fileName), $"Expected previous task to return a " +
                                                                                             $"solution filename. Instead returned {slnObject}");
            var slnName = (string) slnObject;
            Contract.Assert(File.Exists(slnName), $"Expected to find {slnName} on the file system, but couldn't.");

            return await _workspace.OpenSolutionAsync(slnName, _progress, CancellationToken);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem
{
    /// <summary>
    /// Gathers all of the files in a solution and categorizes them.
    /// </summary>
    public sealed class GatherAllFilesInSolutionCmd : BuildCommandBase
    {
        private readonly string _workingDirectory;

        public GatherAllFilesInSolutionCmd(ILogger logger, CancellationToken cancellationToken, string workingDirectory) : base("GatherAllSlnFiles", logger, cancellationToken)
        {
            _workingDirectory = workingDirectory;
        }

        protected override async Task<object> ProcessImpl(Task<object> previousTask)
        {
            var slnObject = await previousTask;
            Contract.Assert(slnObject is Solution s && s != null, $"Expected previous task to return a Solution object, but found {slnObject} instead.");
            var solution = (Solution) slnObject;

            return SolutionAnalyzer.AllSolutionFiles(solution, _workingDirectory);
        }
    }
}

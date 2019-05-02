// -----------------------------------------------------------------------
// <copyright file="GatherAllFilesInSolutionCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    ///     Gathers all of the files in a solution and categorizes them.
    /// </summary>
    public sealed class GatherAllFilesInSolutionCmd : BuildCommandBase<Solution, Dictionary<string, SlnFile>>
    {
        private readonly string _workingDirectory;

        public GatherAllFilesInSolutionCmd(ILogger logger, CancellationToken cancellationToken, string workingDirectory)
            : base("GatherAllSlnFiles", logger, cancellationToken)
        {
            _workingDirectory = workingDirectory;
        }

        protected override async Task<Dictionary<string, SlnFile>> ProcessImpl(Task<Solution> previousTask)
        {
            var slnObject = await previousTask;
            Contract.Assert(slnObject is Solution s && s != null,
                $"Expected previous task to return a Solution object, but found {slnObject} instead.");
            var solution = (Solution) slnObject;

            return SolutionAnalyzer.AllSolutionFiles(solution, _workingDirectory);
        }
    }
}
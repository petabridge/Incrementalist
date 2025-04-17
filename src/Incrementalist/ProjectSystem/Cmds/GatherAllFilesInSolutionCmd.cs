// -----------------------------------------------------------------------
// <copyright file="GatherAllFilesInSolutionCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

#nullable enable
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
    public sealed class GatherAllFilesInSolutionCmd : BuildCommandBase<Solution, Dictionary<AbsolutePath, SlnFile>>
    {
        private readonly AbsolutePath _workingDirectory;

        public GatherAllFilesInSolutionCmd(ILogger logger, CancellationToken cancellationToken,
            AbsolutePath workingDirectory)
            : base("GatherAllSlnFiles", logger, cancellationToken)
        {
            _workingDirectory = workingDirectory;
        }

        protected override async Task<Dictionary<AbsolutePath, SlnFile>> ProcessImpl(Task<Solution> previousTask)
        {
            var slnObject = await previousTask;
            Contract.Assert(slnObject is not null,
                $"Expected previous task to return a Solution object, but found {slnObject} instead.");

            return SolutionAnalyzer.AllSolutionFiles(slnObject, _workingDirectory);
        }
    }
}
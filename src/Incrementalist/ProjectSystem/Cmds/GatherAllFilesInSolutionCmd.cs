// -----------------------------------------------------------------------
// <copyright file="GatherAllFilesInSolutionCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------
#nullable enable
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    ///     Gathers all of the files in a solution and categorizes them.
    /// </summary>
    public sealed class GatherAllFilesInSolutionCmd : BuildCommandBase<SolutionDetails, Dictionary<string, SlnFile>>
    {
        private readonly string _workingDirectory;

        public GatherAllFilesInSolutionCmd(ILogger logger, CancellationToken cancellationToken, string workingDirectory)
            : base("GatherAllSlnFiles", logger, cancellationToken)
        {
            _workingDirectory = workingDirectory;
        }

        protected override async Task<Dictionary<string, SlnFile>> ProcessImpl(Task<SolutionDetails> previousTask)
        {
            var slnObject = await previousTask;
            Contract.Assert(slnObject is not null,
                $"Expected previous task to return a Solution object, but found {slnObject} instead.");

            return SolutionAnalyzer.AllSolutionFiles(slnObject, _workingDirectory);
        }
    }
}
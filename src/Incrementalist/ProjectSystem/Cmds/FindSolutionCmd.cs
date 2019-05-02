// -----------------------------------------------------------------------
// <copyright file="FindSolutionCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <inheritdoc />
    /// <summary>
    ///     Build task for finding a solution in the folder if none was specified on the CLI.
    /// </summary>
    public sealed class FindSolutionCmd : BuildCommandBase<object, IEnumerable<string>>
    {
        private readonly string _folderPath;
        private readonly string _searchFilter;
        private readonly SearchOption? _searchOption;

        public FindSolutionCmd(ILogger logger, string folderPath, CancellationToken token,
            string searchFilter = null, SearchOption? searchOption = null) : base("FindVsSolution", logger, token)
        {
            _folderPath = folderPath;
            _searchFilter = searchFilter;
            _searchOption = searchOption;
        }

        protected override async Task<IEnumerable<string>> ProcessImpl(Task<object> previousTask)
        {
            // previous task should be a no-op
            await previousTask;

            return SolutionFinder.GetSolutions(_folderPath, _searchFilter, _searchOption);
        }
    }
}
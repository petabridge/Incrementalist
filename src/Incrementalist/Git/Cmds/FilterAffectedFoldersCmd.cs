// -----------------------------------------------------------------------
// <copyright file="FilterAffectedFoldersCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Git.Cmds
{
    /// <summary>
    ///     Filters all of the unique folders that contain affected files
    /// </summary>
    public sealed class FilterAffectedFoldersCmd : BuildCommandBase<IEnumerable<string>, Dictionary<string, ICollection<string>>>
    {
        public FilterAffectedFoldersCmd(ILogger logger, CancellationToken cancellationToken) : base(
            "FilterAffectedFiles", logger, cancellationToken)
        {
        }

        protected override async Task<Dictionary<string, ICollection<string>>> ProcessImpl(Task<IEnumerable<string>> previousTask)
        {
            var affectedFiles = await previousTask;

            return affectedFiles
                .Select(x => new { Path = x, Directory = Path.GetDirectoryName(x) })
                .Where(x => x.Directory != null)
                .GroupBy(x => x.Directory)
                .ToDictionary(
                    x => x.Key,
                    grouping => (ICollection<string>)grouping.Select(x => x.Path).Distinct().ToList());
        }
    }
}
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

            return affectedFiles.GroupBy(x => Path.GetDirectoryName(x))
                .ToDictionary(x => x.Key, grouping => (ICollection<string>)grouping.Distinct().ToList());
        }
    }
}
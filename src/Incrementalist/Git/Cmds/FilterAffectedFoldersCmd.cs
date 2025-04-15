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
    public sealed class FilterAffectedFoldersCmd : BuildCommandBase<IEnumerable<AbsolutePath>, Dictionary<AbsolutePath, ICollection<AbsolutePath>>>
    {
        public FilterAffectedFoldersCmd(ILogger logger, CancellationToken cancellationToken) : base(
            "FilterAffectedFolders", logger, cancellationToken)
        {
        }

        protected override async Task<Dictionary<AbsolutePath, ICollection<AbsolutePath>>> ProcessImpl(Task<IEnumerable<AbsolutePath>> previousTask)
        {
            var affectedFiles = await previousTask;

            return affectedFiles.GroupBy(x => new AbsolutePath(Path.GetDirectoryName(x.Path)!))
                .ToDictionary(x => x.Key, grouping => (ICollection<AbsolutePath>)grouping.Distinct().ToList());
        }
    }
}
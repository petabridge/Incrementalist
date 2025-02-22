// -----------------------------------------------------------------------
// <copyright file="FilterAffectedFoldersCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
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
            ArgumentNullException.ThrowIfNull(affectedFiles);

            var validFiles = affectedFiles
                .Where(x => !string.IsNullOrEmpty(x))  // Filter out any null or empty files
                .Select(x => new { Path = x, Directory = Path.GetDirectoryName(x) })
                .Where(x => !string.IsNullOrEmpty(x.Directory))  // Filter out any files without a valid directory
                .ToList();  // Materialize the list to avoid multiple enumeration

            var result = new Dictionary<string, ICollection<string>>();
            
            // Group files by directory and add them to the result dictionary
            foreach (var group in validFiles.GroupBy(x => x.Directory!))  // We know Directory is not null from the Where clause
            {
                var directoryPath = group.Key;  // Key is guaranteed non-null
                var filesInDirectory = group.Select(x => x.Path).Distinct().ToList();
                result.Add(directoryPath, filesInDirectory);
            }

            return result;
        }
    }
}
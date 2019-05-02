using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Git.Cmds
{
    /// <summary>
    /// List all affected files in the working directory.
    /// </summary>
    public sealed class ListAffectedFilesCmd : BuildCommandBase<Repository, IEnumerable<string>>
    {
        private readonly string _targetBranch;

        public ListAffectedFilesCmd(ILogger logger, CancellationToken cancellationToken, string targetBranch) 
            : base("ListAffectedFiles", logger, cancellationToken)
        {
            _targetBranch = targetBranch;
        }

        protected override async Task<IEnumerable<string>> ProcessImpl(Task<Repository> previousTask)
        {
            var repository = await previousTask;

            return DiffHelper.ChangedFiles(repository, _targetBranch);
        }
    }

    /// <summary>
    /// Filters all of the unique folders that contain affected files
    /// </summary>
    public sealed class FilterAffectedFoldersCmd : BuildCommandBase<IEnumerable<string>, IEnumerable<string>>
    {
        public FilterAffectedFoldersCmd(ILogger logger, CancellationToken cancellationToken) : base("FilterAffectedFiles", logger, cancellationToken)
        {
        }

        protected override async Task<IEnumerable<string>> ProcessImpl(Task<IEnumerable<string>> previousTask)
        {
            var affectedFiles = await previousTask;

            return affectedFiles.Select(Path.GetDirectoryName).Distinct();
        }
    }
}

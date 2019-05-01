using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Git;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem
{
    /// <summary>
    /// Filter the previously discovered <see cref="SlnFile"/>s by whether or not
    /// they were touched via the GitDiff.
    /// </summary>
    public sealed class FilterByAffectedFiles : BuildCommandBase
    {
        private readonly string _targetGitBranch;
        private readonly string _workingDirectory;

        public FilterByAffectedFiles(ILogger logger, CancellationToken cancellationToken, string workingDirectory, string targetGitBranch) 
            : base("FilterSlnFilesByGitDiff", logger, cancellationToken)
        {
            _workingDirectory = workingDirectory;
            _targetGitBranch = targetGitBranch;
        }

        protected override async Task<object> ProcessImpl(Task<object> previousTask)
        {
            var fileDictObj = await previousTask;
            Contract.Assert(fileDictObj is Dictionary<string, SlnFile>, $"Expected Dictionary<string, SlnFile>, but found [{fileDictObj}] instead.");

            var fileDict = (Dictionary<string, SlnFile>) fileDictObj;

            var repoResult = GitRunner.FindRepository(_workingDirectory);
            if (!repoResult.foundRepo)
            {
                var errMsg = $"Unable to find suitable git repository starting in directory {_workingDirectory}";
                Logger.LogError(errMsg);
                throw new InvalidOperationException(errMsg);
            }

            var repo = repoResult.repo;
            var affectedFiles = DiffHelper.ChangedFiles(repo, _targetGitBranch);

            // filter out any files that aren't affected by the diff
            var newDict = new Dictionary<string, SlnFile>();
            foreach (var file in affectedFiles)
            {
                // this file is in the solution
                if (fileDict.ContainsKey(file))
                {
                    newDict[file] = fileDict[file];
                }
            }

            return newDict;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LibGit2Sharp;

namespace Incrementalist.Git
{
    /// <summary>
    /// Used to run all of the commands needed to execute a query.
    /// </summary>
    public sealed class GitRunner
    {
        /// <summary>
        /// Find the repository and the full path of the base directory.
        /// </summary>
        /// <param name="targetDirectory">Optional. The directory to search inside of. Defaults to <see cref="Directory.GetCurrentDirectory"/> otherwise.</param>
        /// <returns>A tuple containing the repository, the base directory (parent directory of the .git folder,) and a boolean flag indicating whether or not
        /// the search was successful.</returns>
        public (Repository repo, string baseDirectory, bool foundRepo) FindRepository(string targetDirectory = null)
        {
            var repoPath = Repository.Discover(targetDirectory ?? Directory.GetCurrentDirectory());
            if (string.IsNullOrEmpty(repoPath))
                return (null, null, false);

            return (new Repository(repoPath), Directory.GetParent(repoPath).Parent.FullName, true);
        }
    }
}

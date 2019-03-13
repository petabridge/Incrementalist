using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using LibGit2Sharp;

namespace Incrementalist.Tests.Helpers
{
    public class DisposableRepository : IDisposable
    {
        /// <summary>
        /// Since it might take a few tries to delete the Git repository.
        /// </summary>
        private const int MaxDeleteAttempts = 5;

        /// <summary>
        /// Needed to create repositories in random, temporary directories.
        /// </summary>
        /// <returns>The path to a temporary, random directory.</returns>
        public static string CreateTempDirectory()
        {
            var dirPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Path.GetFileNameWithoutExtension(System.IO.Path.GetRandomFileName()));
            Directory.CreateDirectory(dirPath);
            return dirPath;
        }

        public DisposableRepository() : this(CreateTempDirectory()) { }

        public DisposableRepository(string basePath)
        {
            BasePath = basePath;
            Init();
        }

        private void Init()
        {
            var repoPath = Repository.Init(BasePath);
            Repository = new Repository(repoPath);
            var sig = CreateSignature();
            Repository.Commit("First", sig, sig);
            //Repository.CreateBranch("master"); // setup the master branch initially
        }

        public string BasePath { get; }

        public Repository Repository { get; private set; }

        /// <summary>
        /// Create a new branch inside this repository.
        /// </summary>
        /// <param name="branchName">The name of the branch to be created.</param>
        /// <returns>The current <see cref="DisposableRepository"/>.</returns>
        public DisposableRepository CreateBranch(string branchName)
        {
            Repository.CreateBranch(branchName);
            return this;
        }

        /// <summary>
        /// Checks out the specified branch, assuming it exists.
        /// </summary>
        /// <param name="branchName">The name of the branch to be checked out.</param>
        /// <returns>The current <see cref="DisposableRepository"/>.</returns>
        public DisposableRepository CheckoutBranch(string branchName)
        {
            var branch = Repository.Branches[branchName];
            var currentBranch = Commands.Checkout(Repository, branch);
            return this;
        }

        /// <summary>
        /// Add a new file to the repository.
        /// </summary>
        /// <param name="fileName">The name of the file to add or overwrite.</param>
        /// <param name="fileText">The content of the file.</param>
        /// <returns>The current <see cref="DisposableRepository"/>.</returns>
        public DisposableRepository WriteFile(string fileName, string fileText)
        {
            var filePath = Path.Combine(BasePath, fileName);
            File.WriteAllText(filePath, fileText);
            Repository.Index.Add(fileName);
            return this;
        }

        /// <summary>
        /// Create a new commit inside the <see cref="Repository"/>.
        /// </summary>
        /// <param name="commitMessage">The commit message.</param>
        /// <param name="author">Optional. The signature of the author performing the commit.</param>
        /// <returns>The current <see cref="DisposableRepository"/>.</returns>
        public DisposableRepository Commit(string commitMessage, Signature author = null)
        {
            var committer = author ?? CreateSignature();
            Repository.Commit(commitMessage, committer, committer);
            return this;
        }

        public static Signature CreateSignature(string name = null, string email = null)
        {
            return new Signature(name ?? "Fuber", email ?? "fuber@petabridge.com", DateTimeOffset.UtcNow);
        }

        public void Dispose()
        {
            Repository?.Dispose();
            for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
            {
                try
                {
                    Directory.Delete(BasePath, true);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt < MaxDeleteAttempts)
                    {
                        // some exponential backoff here
                        Thread.Sleep(100 + (int)Math.Pow(10, attempt - 1));
                    }
                }
            }
           
        }
    }
}

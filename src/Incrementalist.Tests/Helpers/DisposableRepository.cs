// -----------------------------------------------------------------------
// <copyright file="DisposableRepository.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using LibGit2Sharp;

namespace Incrementalist.Tests.Helpers
{
    public sealed class DisposableRepository : IDisposable
    {
        public const string GitIgnoreContent = """
                                               # Build results
                                               [Dd]ebug/
                                               [Dd]ebugPublic/
                                               [Rr]elease/
                                               [Rr]eleases/
                                               x64/
                                               x86/
                                               [Ww][Ii][Nn]32/
                                               [Aa][Rr][Mm]/
                                               [Aa][Rr][Mm]64/
                                               bld/
                                               [Bb]in/
                                               [Oo]bj/
                                               [Ll]og/
                                               [Ll]ogs/

                                               # Visual Studio 2015/2017 cache/options directory
                                               .vs/
                                               # Uncomment if you have tasks that create the project's static files in wwwroot
                                               #wwwroot/
                                               """;

        public const string GitIgnoreFileName = ".gitignore";

        /// <summary>
        ///     Since it might take a few tries to delete the Git repository.
        /// </summary>
        private const int MaxDeleteAttempts = 5;

        public DisposableRepository() : this(CreateTempDirectory())
        {
        }

        public DisposableRepository(AbsolutePath basePath)
        {
            BasePath = basePath;
            Init();
        }

        public AbsolutePath BasePath { get; }

        // Gets created via CTOR method call, so can't be null unless catastrophic failure
        public Repository Repository { get; private set; } = null!;

        public void Dispose()
        {
            Repository?.Dispose();
            for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
                try
                {
                    Directory.Delete(BasePath.Path, true);
                    return;
                }
                catch (Exception)
                {
                    if (attempt < MaxDeleteAttempts) Thread.Sleep(100 + (int)Math.Pow(10, attempt - 1));
                }
        }

        /// <summary>
        ///     Needed to create repositories in random, temporary directories.
        /// </summary>
        /// <returns>The path to a temporary, random directory.</returns>
        public static AbsolutePath CreateTempDirectory()
        {
            var dirPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(dirPath);
            return new AbsolutePath(dirPath);
        }

        private void Init()
        {
            var repoPath = Repository.Init(BasePath.Path);
            Repository = new Repository(repoPath);
            var sig = CreateSignature();
            // add a .gitignore file to the repository immediately
            WriteFile(GitIgnoreFileName, GitIgnoreContent);
            Repository.Commit("First", sig, sig);
            //Repository.CreateBranch("master"); // setup the master branch initially
        }

        /// <summary>
        ///     Create a new branch inside this repository.
        /// </summary>
        /// <param name="branchName">The name of the branch to be created.</param>
        /// <returns>The current <see cref="DisposableRepository" />.</returns>
        public DisposableRepository CreateBranch(string branchName)
        {
            Repository.CreateBranch(branchName);
            return this;
        }

        /// <summary>
        ///     Checks out the specified branch, assuming it exists.
        /// </summary>
        /// <param name="branchName">The name of the branch to be checked out.</param>
        /// <returns>The current <see cref="DisposableRepository" />.</returns>
        public DisposableRepository CheckoutBranch(string branchName)
        {
            var branch = Repository.Branches[branchName];
            var currentBranch = LibGit2Sharp.Commands.Checkout(Repository, branch);
            return this;
        }

        /// <summary>
        ///     Add a new file to the repository.
        /// </summary>
        /// <param name="fileName">The name of the file to add or overwrite.</param>
        /// <param name="fileText">The content of the file.</param>
        /// <returns>The current <see cref="DisposableRepository" />.</returns>
        public DisposableRepository WriteFile(string fileName, string fileText)
        {
            var filePath = Path.Combine(BasePath.Path, fileName);
            File.WriteAllText(filePath, fileText);
            LibGit2Sharp.Commands.Stage(Repository, filePath);
            return this;
        }

        public DisposableRepository WriteSolution(TestSolutionModel testSolutionModel)
        {
            // need to traverse the solution and write the entire graph
            // of projects to disk

            // write the solution first
            var solutionText = testSolutionModel.Serialize();
            WriteFile(testSolutionModel.FileName.Name, solutionText);

            foreach (var c in testSolutionModel.FileStructure)
            {
                switch (c)
                {
                    case SolutionFolder dir:
                        ProcessFolder(dir);
                        break;
                    case ProjectModel project:
                        ProcessProject(project);
                        break;
                    case SampleFile sampleFile:
                        WriteFile(sampleFile.Name, sampleFile.Content);
                        break;
                }
            }

            return this;

            void ProcessProject(ProjectModel project)
            {
                var serializedProject = project.Serialize();
                CreateDirectory(project.RelativePathFromRepository);
                WriteFile(project.CompletePath, serializedProject);

                foreach (var file in project.IncludedFiles)
                {
                    var fullPath = Path.Combine(project.RelativePathFromRepository, file.Name);
                    WriteFile(fullPath, file.Content);
                }
            }

            void ProcessFolder(SolutionFolder folder)
            {
                CreateDirectory(folder.Name);
                foreach (var item in folder.Items)
                {
                    switch (item)
                    {
                        case SolutionFolder subDir:
                            ProcessFolder(subDir);
                            break;
                        case ProjectModel project:
                            ProcessProject(project);
                            break;
                        case SampleFile sampleFile:
                            var path = Path.Combine(folder.Name, sampleFile.Name);
                            WriteFile(path, sampleFile.Content);
                            break;
                    }
                }
            }
        }

        public DisposableRepository CreateDirectory(string directoryName)
        {
            var dirPath = Path.Combine(BasePath.Path, directoryName);
            Directory.CreateDirectory(dirPath);
            return this;
        }

        /// <summary>
        /// Adds or updates sample fime in the repository
        /// </summary>
        /// <param name="sampleFile">File info source</param>
        /// <returns>The current <see cref="DisposableRepository" />.</returns>
        public DisposableRepository WriteFile(SampleFile sampleFile) =>
            WriteFile(sampleFile.Name, sampleFile.Content);

        /// <summary>
        ///     Delete an existing file from the repository.
        /// </summary>
        /// <param name="fileName">The name of the file to delete.</param>
        /// <returns>The current <see cref="DisposableRepository" />.</returns>
        public DisposableRepository DeleteFile(string fileName)
        {
            var filePath = Path.Combine(BasePath.Path, fileName);
            File.Delete(fileName);
            LibGit2Sharp.Commands.Remove(Repository, filePath);
            return this;
        }

        public DisposableRepository AddOrModifyProjectFile(ProjectModel project, SampleFile sampleFile)
        {
            var filePath = Path.Combine(project.RelativePathFromRepository, sampleFile.Name);

            // this will overwrite the file if it already exists
            WriteFile(filePath, sampleFile.Content);
            return this;
        }

        /// <summary>
        ///     Create a new commit inside the <see cref="Repository" />.
        /// </summary>
        /// <param name="commitMessage">The commit message.</param>
        /// <param name="author">Optional. The signature of the author performing the commit.</param>
        /// <returns>The current <see cref="DisposableRepository" />.</returns>
        public DisposableRepository Commit(string commitMessage, Signature? author = null)
        {
            var committer = author ?? CreateSignature();
            Repository.Commit(commitMessage, committer, committer);
            return this;
        }

        public static Signature CreateSignature(string? name = null, string? email = null)
        {
            return new Signature(name ?? "Fuber", email ?? "fuber@petabridge.com", DateTimeOffset.UtcNow);
        }
    }
}
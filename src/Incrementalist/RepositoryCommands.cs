using LibGit2Sharp;

namespace Incrementalist
{
    /// <summary>
    /// Quick commands for working with Git repositories inside Incrementalist.
    /// </summary>
    public class RepositoryCommands
    {
        private IRepository _repository;

        /// <summary>
        /// Creates a new <see cref="RepositoryCommands"/> instance provided that there
        /// is a working repository in the <see cref="workingDirectory"/>.
        /// </summary>
        /// <param name="workingDirectory">The working directory that Incrementalist will target
        /// for performing its analysis. Should contain a git repository or be part of one.</param>
        public RepositoryCommands(string workingDirectory)
        {
            _repository = new Repository(workingDirectory);
        }

        public bool HaveRepositoryInFolder => !_repository.Info.IsBare;
    }
}

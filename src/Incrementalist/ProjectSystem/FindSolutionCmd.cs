using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem
{
    /// <inheritdoc />
    /// <summary>
    /// Build task for finding a solution in the folder if none was specified on the CLI.
    /// </summary>
    public sealed class FindSolutionCmd : BuildCommandBase
    {
        private readonly string _folderPath;
        private readonly string _searchFilter;
        private readonly SearchOption? _searchOption;

        public FindSolutionCmd(ILogger logger, string folderPath, CancellationToken token, 
            string searchFilter = null, SearchOption? searchOption = null) : base("FindVsSolution", logger, token)
        {
            _folderPath = folderPath;
            _searchFilter = searchFilter;
            _searchOption = searchOption;
        }

        protected override async Task<object> ProcessImpl(Task<object> previousTask)
        {
            // previous task should be a no-op
            await previousTask;

            return SolutionFinder.GetSolutions(_folderPath, _searchFilter, _searchOption);
        }
    }
}
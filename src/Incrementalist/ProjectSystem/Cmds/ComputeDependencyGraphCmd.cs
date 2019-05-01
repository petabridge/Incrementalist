using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    /// Computes the longest dependency graph from all of the affected files
    /// and emits a topologically sorted set of project file names to be used during testing.
    /// </summary>
    public sealed class ComputeDependencyGraphCmd : BuildCommandBase<Dictionary<string, SlnFile>, IEnumerable<string>>
    {
        private readonly Solution _solution;

        public ComputeDependencyGraphCmd(ILogger logger, CancellationToken cancellationToken, Solution solution) : base("ResolveSlnDependencyGraph", logger, cancellationToken)
        {
            _solution = solution;
        }

        protected override async Task<IEnumerable<string>> ProcessImpl(Task<Dictionary<string, SlnFile>> previousTask)
        {
            var affectedSlnFiles = await previousTask;

            // bail out early if we don't have any affected projects
            if(affectedSlnFiles.Count == 0)
                return new List<string>();

            var ds = _solution.GetProjectDependencyGraph();

            // NOTE: what happens if the SLN file is modified? Return everything?
            var uniqueProjectIds = affectedSlnFiles.Select(x => x.Value.ProjectId).Distinct().ToList();
            var graphs = uniqueProjectIds.ToDictionary(x => x,
                v => ds.GetProjectsThatTransitivelyDependOnThisProject(v).ToList());

            var longestGraph = graphs.OrderByDescending(x => x.Value.Count).FirstOrDefault();

            var results = new HashSet<string> {_solution.GetProject(longestGraph.Key).FilePath};
            foreach (var p in longestGraph.Value)
            {
                results.Add(_solution.GetProject(p).FilePath);
            }

            return results;
        }
    }
}

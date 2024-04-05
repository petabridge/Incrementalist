// -----------------------------------------------------------------------
// <copyright file="ComputeDependencyGraphCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    /// Value type for a solution file
    /// </summary>
    /// <param name="FilePath">The underlying path to the solution file</param>
    public sealed record AffectedSolution(string FilePath);

    /// <summary>
    /// Value type for a project file
    /// </summary>
    /// <param name="FilePath">The underlying path to the project file</param>
    public sealed record AffectedProject(string FilePath);

    /// <summary>
    /// Report on a project's dependency graph
    /// </summary>
    /// <param name="AffectedProject">The affected project file containing the changes.</param>
    /// <param name="DependentProjects">The set of affected projects that depend on <see cref="AffectedProject"/>.</param>
    public sealed record AffectedProjectDependencyGraph(
        AffectedProject AffectedProject,
        ImmutableHashSet<AffectedProject> DependentProjects);

    /// <summary>
    /// Report on the Incrementalist analysis of an entire solution
    /// </summary>
    /// <param name="Solution">The solution we analyzed.</param>
    /// <param name="AffectedGraphs">All impacted graphs - can be empty.</param>
    /// <param name="ProcessEntireSolution">In some special cases, we're just going to process the entire solution instead.</param>
    public sealed record SolutionAnalysisResult(
        AffectedSolution Solution,
        ImmutableHashSet<AffectedProjectDependencyGraph> AffectedGraphs,
        bool ProcessEntireSolution)
    {
        public int Count => AffectedGraphs.Sum(c => c.DependentProjects.Count);
    }

    /// <summary>
    ///     Computes the longest dependency graph from all of the affected files
    ///     and emits a topologically sorted set of project file names to be used during testing.
    /// </summary>
    public sealed class
        ComputeDependencyGraphCmd : BuildCommandBase<Dictionary<string, SlnFile>, SolutionAnalysisResult>
    {
        private readonly Solution _solution;

        public ComputeDependencyGraphCmd(ILogger logger, Solution solution, CancellationToken cancellationToken) : base(
            "ResolveSlnDependencyGraph", logger, cancellationToken)
        {
            _solution = solution;
        }

        protected override async Task<SolutionAnalysisResult> ProcessImpl(
            Task<Dictionary<string, SlnFile>> previousTask)
        {
            var affectedSolution = new AffectedSolution(_solution.FilePath);
            var analysisResult = new SolutionAnalysisResult(affectedSolution,
                ImmutableHashSet<AffectedProjectDependencyGraph>.Empty, false);
            var affectedSlnFiles = await previousTask;

            // bail out early if we don't have any affected projects
            if (affectedSlnFiles.Count == 0)
                return analysisResult;

            /*
             * Special case: in instances where the project files themselves are modified,
             * we there might be multiple ProjectIds in the case of a multi-targeted solution.
             *
             * We have to gather up each unique project file separately in this case.
             */
            var additionalProjectIds = new List<ProjectId>();
            if (affectedSlnFiles.Any(x => x.Value.FileType == FileType.Project))
            {
                foreach (var proj in affectedSlnFiles.Where(x => x.Value.FileType == FileType.Project))
                    additionalProjectIds.AddRange(_solution.Projects.Where(x => x.FilePath.Equals(proj.Key))
                        .Select(x => x.Id));
            }

            var ds = _solution.GetProjectDependencyGraph();

            // Special case: if the solution itself is modified, return all projects
            if (affectedSlnFiles.ContainsKey(_solution.FilePath))
            {
                var affectedProjects =
                    _solution.Projects.Select(x => new AffectedProject(x.FilePath)).ToImmutableHashSet();
                return new SolutionAnalysisResult(affectedSolution,
                    affectedProjects
                        .Select(x => new AffectedProjectDependencyGraph(x, ImmutableHashSet<AffectedProject>.Empty.Add(x)))
                        .ToImmutableHashSet(), true);
            }

            var uniqueProjectIds = affectedSlnFiles.Select(x => x.Value.ProjectId).Concat(additionalProjectIds)
                .Distinct().ToList();
            var graphs = uniqueProjectIds.ToDictionary(x => x,
                v => ds.GetProjectsThatTransitivelyDependOnThisProject(v).ToList());

            var independentGraphs = graphs.Where(x => !IsGraphContained(x.Key, graphs));

            // idempotently filter out duplicates - same projectID can show up multiple times for a multi-target build
            var finalResultSet = new Dictionary<string, ICollection<string>>();
            foreach (var r in independentGraphs)
            {
                var projectPath = GetProjectFilePath(r.Key);
                /*
                 * BUGFIX for https://github.com/petabridge/Incrementalist/issues/63
                 *
                 */
                if (finalResultSet.ContainsKey(projectPath))
                {
                    var exitingPaths = finalResultSet[projectPath];
                    var newPaths = PrepareProjectPaths(r.Key, r.Value);
                    finalResultSet[projectPath] = exitingPaths.Concat(newPaths).Distinct().ToList();
                }
                else
                {
                    finalResultSet[projectPath] = PrepareProjectPaths(r.Key, r.Value);
                }
            }

            // turn the dictionary into a set of strongly typed objects
            analysisResult = new SolutionAnalysisResult(affectedSolution,
                finalResultSet.Select(x => new AffectedProjectDependencyGraph(new AffectedProject(x.Key),
                    x.Value.Select(y => new AffectedProject(y)).ToImmutableHashSet())).ToImmutableHashSet(), false);

            return analysisResult;

            /*
             * Next: check to see if there any overlapping graphs and remove those from the final set
             */
            bool IsGraphContained(ProjectId root, Dictionary<ProjectId, List<ProjectId>> otherGraphs)
            {
                return otherGraphs.Where(x => !x.Key.Equals(root))
                    .Any(nonRootGraph => nonRootGraph.Value.Contains(root));
            }

            ICollection<string> PrepareProjectPaths(ProjectId root, IEnumerable<ProjectId> graph)
            {
                var results = new HashSet<string> { _solution.GetProject(root).FilePath };
                foreach (var p in graph)
                {
                    results.Add(GetProjectFilePath(p));
                }

                return results;
            }

            string GetProjectFilePath(ProjectId project)
            {
                return _solution.GetProject(project).FilePath;
            }
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="ComputeDependencyGraphCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    ///     Computes the longest dependency graph from all the affected files
    ///     and emits a topologically sorted set of project file names to be used during testing.
    /// </summary>
    public sealed class
        ComputeDependencyGraphCmd : BuildCommandBase<Dictionary<AbsolutePath, SlnFile[]>,
        Dictionary<AbsolutePath, ICollection<AbsolutePath>>>
    {
        private readonly Solution _solution;

        public ComputeDependencyGraphCmd(ILogger logger, CancellationToken cancellationToken, Solution solution) : base(
            "ResolveSlnDependencyGraph", logger, cancellationToken)
        {
            _solution = solution;
        }

        protected override async Task<Dictionary<AbsolutePath, ICollection<AbsolutePath>>> ProcessImpl(
            Task<Dictionary<AbsolutePath, SlnFile[]>> previousTask)
        {
            var affectedSlnFiles = await previousTask;

            // bail out early if we don't have any affected projects
            if (affectedSlnFiles.Count == 0)
            {
                Logger.LogDebug("No affected projects found. Skipping dependency graph computation.");
                return new Dictionary<AbsolutePath, ICollection<AbsolutePath>>();
            }


            /*
             * Special case: in instances where the project files themselves are modified,
             * there might be multiple ProjectIds in the case of a multi-targeted solution.
             *
             * We have to gather each unique project file separately in this case.
             */
            List<Project> additionalProjects = [];
            if (affectedSlnFiles.Any(x => x.Value.Any(f => f.FileType == FileType.Project)))
            {
                foreach (var proj in affectedSlnFiles.Where(x => x.Value.Any(f => f.FileType == FileType.Project)))
                    additionalProjects.AddRange(_solution.Projects.Where(x => x.FilePath.Equals(proj.Key)));
            }

            if (additionalProjects.Count > 0)
                Logger.LogDebug("Found {Count} additional project IDs in affected files.", additionalProjects.Count);

            // Special case: if the solution itself is modified, return all projects
            if (affectedSlnFiles.ContainsKey(_solution.FilePath))
            {
                Logger.LogDebug("Solution file modified. Returning all projects.");
                return new Dictionary<AbsolutePath, ICollection<AbsolutePath>>
                {
                    {
                        _solution.FilePath,
                        _solution.Projects.Select(x => x.FilePath).ToList()
                    }
                };
            }

            var uniqueProjects = affectedSlnFiles
                .Where(c => c.Value.All(f => f.Project != null))
                .SelectMany(x => x.Value.Select(f => f.Project!)).Concat(additionalProjects)
                .Distinct().ToList();

            Logger.LogDebug("Evaluating {Count} unique project IDs.", uniqueProjects.Count);

            var graphs = uniqueProjects.ToDictionary(x => x, v => _solution.GetTransitiveProjects(v));

            var independentGraphs = graphs.Where(x => !IsGraphContained(x.Key, graphs));

            // idempotently filter out duplicates - same projectID can show up multiple times for a multi-target build
            var finalResultSet = new Dictionary<AbsolutePath, ICollection<AbsolutePath>>();
            foreach (var r in independentGraphs)
            {
                var projectPath = r.Key.FilePath;

                /*
                 * BUGFIX for https://github.com/petabridge/Incrementalist/issues/63
                 *
                 */
                if (finalResultSet.TryGetValue(projectPath, out var exitingPaths))
                {
                    var newPaths = PrepareProjectPaths(r.Key, r.Value);
                    finalResultSet[projectPath] = exitingPaths.Concat(newPaths).Distinct().ToList();
                }
                else
                {
                    finalResultSet[projectPath] = PrepareProjectPaths(r.Key, r.Value);
                }
            }

            return finalResultSet;

            /*
             * Next: check to see if there are any overlapping graphs and remove those from the final set
             */
            bool IsGraphContained(Project root, Dictionary<Project, IReadOnlyCollection<Project>> otherGraphs)
            {
                return otherGraphs.Where(x => !x.Key.Equals(root))
                    .Any(nonRootGraph => nonRootGraph.Value.Contains(root));
            }

            ICollection<AbsolutePath> PrepareProjectPaths(Project root, IEnumerable<Project> graph)
            {
                var results = new HashSet<AbsolutePath> { root.FilePath };
                foreach (var p in graph)
                {
                    results.Add(p.FilePath);
                }

                return results;
            }
        }
    }
}
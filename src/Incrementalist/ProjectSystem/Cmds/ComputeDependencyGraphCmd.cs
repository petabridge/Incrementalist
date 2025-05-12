// -----------------------------------------------------------------------
// <copyright file="ComputeDependencyGraphCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    ///     Computes the longest dependency graph from all the affected files
    ///     and emits a topologically sorted set of project file names to be used during testing.
    /// </summary>
    public sealed class
        ComputeDependencyGraphCmd : BuildCommandBase<Dictionary<AbsolutePath, SlnFile>,
        Dictionary<AbsolutePath, ICollection<AbsolutePath>>>
    {
        private readonly Solution _solution;

        public ComputeDependencyGraphCmd(ILogger logger, CancellationToken cancellationToken, Solution solution) : base(
            "ResolveSlnDependencyGraph", logger, cancellationToken)
        {
            _solution = solution;
        }

        protected override async Task<Dictionary<AbsolutePath, ICollection<AbsolutePath>>> ProcessImpl(
            Task<Dictionary<AbsolutePath, SlnFile>> previousTask)
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
             * we there might be multiple ProjectIds in the case of a multi-targeted solution.
             *
             * We have to gather up each unique project file separately in this case.
             */
            List<ProjectId> additionalProjectIds = [];
            if (affectedSlnFiles.Any(x => x.Value.FileType == FileType.Project))
            {
                foreach (var proj in affectedSlnFiles.Where(x => x.Value.FileType == FileType.Project))
                    additionalProjectIds.AddRange(_solution.Projects
                        .Where(x => x.FilePath != null && x.FilePath.Equals(proj.Key.Path)).Select(x => x.Id));
            }

            if (additionalProjectIds.Count > 0)
                Logger.LogDebug("Found {Count} additional project IDs in affected files.", additionalProjectIds.Count);

            var ds = _solution.GetProjectDependencyGraph();

            // Special case: if the solution itself is modified, return all projects
            if (_solution.FilePath != null && affectedSlnFiles.ContainsKey(new AbsolutePath(_solution.FilePath)))
            {
                Logger.LogDebug("Solution file modified. Returning all projects.");
                return new Dictionary<AbsolutePath, ICollection<AbsolutePath>>
                {
                    {
                        new AbsolutePath(_solution.FilePath),
                        _solution.Projects.Where(c => c.FilePath != null).Select(x => new AbsolutePath(x.FilePath!))
                            .ToList()!
                    }
                };
            }

            var uniqueProjectIds = affectedSlnFiles
                .Where(c => c.Value.ProjectId != null)
                .Select(x => x.Value.ProjectId!).Concat(additionalProjectIds)
                .Distinct().ToList();

            Logger.LogDebug("Evaluating {Count} unique project IDs.", uniqueProjectIds.Count);

            var graphs = uniqueProjectIds.ToDictionary(x => x,
                v => ds.GetProjectsThatTransitivelyDependOnThisProject(v).ToList());

            var independentGraphs = graphs.Where(x => !IsGraphContained(x.Key, graphs));

            // idempotently filter out duplicates - same projectID can show up multiple times for a multi-target build
            var finalResultSet = new Dictionary<AbsolutePath, ICollection<AbsolutePath>>();
            foreach (var r in independentGraphs)
            {
                var projectPath = GetProjectFilePath(r.Key);

                if (projectPath == null)
                    continue;

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
             * Next: check to see if there any overlapping graphs and remove those from the final set
             */
            bool IsGraphContained(ProjectId root, Dictionary<ProjectId, List<ProjectId>> otherGraphs)
            {
                return otherGraphs.Where(x => !x.Key.Equals(root))
                    .Any(nonRootGraph => nonRootGraph.Value.Contains(root));
            }

            ICollection<AbsolutePath> PrepareProjectPaths(ProjectId root, IEnumerable<ProjectId> graph)
            {
                var rootProject = _solution.GetProject(root);
                if (rootProject?.FilePath == null)
                    return Array.Empty<AbsolutePath>();

                var results = new HashSet<AbsolutePath> { new AbsolutePath(rootProject.FilePath) };
                foreach (var p in graph)
                {
                    var projectFilePath = GetProjectFilePath(p);
                    if (projectFilePath != null)
                        results.Add(projectFilePath);
                }

                return results;
            }

            AbsolutePath? GetProjectFilePath(ProjectId project)
            {
                var path = _solution.GetProject(project)?.FilePath;
                if (path == null)
                    return null;
                var projectFilePath = new AbsolutePath(path);
                return projectFilePath;
            }
        }
    }
}
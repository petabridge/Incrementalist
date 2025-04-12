// -----------------------------------------------------------------------
// <copyright file="ComputeDependencyGraphCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
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
        ComputeDependencyGraphCmd : BuildCommandBase<Dictionary<string, SlnFile>,
        Dictionary<string, ICollection<string>>>
    {
        private readonly Solution _solution;

        public ComputeDependencyGraphCmd(ILogger logger, CancellationToken cancellationToken, Solution solution) : base(
            "ResolveSlnDependencyGraph", logger, cancellationToken)
        {
            _solution = solution;
        }

        protected override async Task<Dictionary<string, ICollection<string>>> ProcessImpl(
            Task<Dictionary<string, SlnFile>> previousTask)
        {
            var affectedSlnFiles = await previousTask;

            // bail out early if we don't have any affected projects
            if (affectedSlnFiles.Count == 0)
                return new Dictionary<string, ICollection<string>>();

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
                        .Where(x => x.FilePath != null && x.FilePath.Equals(proj.Key)).Select(x => x.Id));
            }

            var ds = _solution.GetProjectDependencyGraph();

            // Special case: if the solution itself is modified, return all projects
            if (_solution.FilePath != null && affectedSlnFiles.ContainsKey(_solution.FilePath))
            {
                return new Dictionary<string, ICollection<string>>
                {
                    {
                        _solution.FilePath,
                        _solution.Projects.Where(c => c.FilePath != null).Select(x => x.FilePath).ToList()!
                    }
                };
            }

            var uniqueProjectIds = affectedSlnFiles
                .Where(c => c.Value.ProjectId != null)
                .Select(x => x.Value.ProjectId!).Concat(additionalProjectIds)
                .Distinct().ToList();
            var graphs = uniqueProjectIds.ToDictionary(x => x,
                v => ds.GetProjectsThatTransitivelyDependOnThisProject(v).ToList());

            var independentGraphs = graphs.Where(x => !IsGraphContained(x.Key, graphs));

            // idempotently filter out duplicates - same projectID can show up multiple times for a multi-target build
            var finalResultSet = new Dictionary<string, ICollection<string>>();
            foreach (var r in independentGraphs)
            {
                var projectPath = GetProjectFilePath(r.Key);
                
                if(projectPath == null)
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

            ICollection<string> PrepareProjectPaths(ProjectId root, IEnumerable<ProjectId> graph)
            {
                var rootProject = _solution.GetProject(root);
                if (rootProject?.FilePath == null)
                    return Array.Empty<string>();
                
                var results = new HashSet<string> { rootProject.FilePath };
                foreach (var p in graph)
                {
                    var projectFilePath = GetProjectFilePath(p);
                    if (projectFilePath != null)
                        results.Add(projectFilePath);
                }

                return results;
            }

            string? GetProjectFilePath(ProjectId project)
            {
                return _solution.GetProject(project)?.FilePath;
            }
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="ComputeDependencyGraphCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

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
    ///     Computes the longest dependency graph from all of the affected files
    ///     and emits a topologically sorted set of project file names to be used during testing.
    /// </summary>
    public sealed class ComputeDependencyGraphCmd : BuildCommandBase<Dictionary<string, SlnFile>, Dictionary<string, ICollection<string>>>
    {
        private readonly Solution _solution;

        public ComputeDependencyGraphCmd(ILogger logger, CancellationToken cancellationToken, Solution solution) : base(
            "ResolveSlnDependencyGraph", logger, cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(solution);
            _solution = solution;
        }

        protected override async Task<Dictionary<string, ICollection<string>>> ProcessImpl(Task<Dictionary<string, SlnFile>> previousTask)
        {
            var affectedSlnFiles = await previousTask;
            ArgumentNullException.ThrowIfNull(affectedSlnFiles);

            // bail out early if we don't have any affected projects
            if (affectedSlnFiles.Count == 0)
                return new Dictionary<string, ICollection<string>>();

            /*
             * Special case: in instances where the project files themselves are modified,
             * we there might be multiple ProjectIds in the case of a multi-targeted solution.
             * 
             * We have to gather up each unique project file separately in this case.
             */
            var additionalProjectIds = new List<ProjectId>();
            if(affectedSlnFiles.Any(x => x.Value.FileType == FileType.Project))
            {
                foreach(var proj in affectedSlnFiles.Where(x => x.Value.FileType == FileType.Project))
                {
                    var matchingProjects = _solution.Projects
                        .Where(x => x.FilePath != null && x.FilePath.Equals(proj.Key))
                        .Select(x => x.Id);
                    additionalProjectIds.AddRange(matchingProjects);
                }
            }

            var ds = _solution.GetProjectDependencyGraph();

            // Special case: if the solution itself is modified, return all projects
            if (_solution.FilePath != null && affectedSlnFiles.ContainsKey(_solution.FilePath))
            {
                var projectPaths = _solution.Projects
                    .Where(x => x.FilePath != null)
                    .Select(x => x.FilePath!)  // We know FilePath is not null from Where clause
                    .ToList();
                return new Dictionary<string, ICollection<string>>() { { _solution.FilePath, projectPaths } };
            }

            string? GetProjectFilePath(ProjectId projectId)
            {
                var project = _solution.GetProject(projectId);
                return project?.FilePath;
            }

            var uniqueProjectIds = affectedSlnFiles
                .Select(x => x.Value.ProjectId)
                .Where(x => x != null)
                .Concat(additionalProjectIds)
                .Distinct()
                .ToList();

            var graphs = uniqueProjectIds
                .ToDictionary(
                    x => x!,  // We know x is not null due to Where clause above
                    v => ds.GetProjectsThatTransitivelyDependOnThisProject(v!).ToList());

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
                var results = new HashSet<string>();
                var rootPath = GetProjectFilePath(root);
                if (!string.IsNullOrEmpty(rootPath))
                    results.Add(rootPath);

                foreach (var p in graph)
                {
                    var path = GetProjectFilePath(p);
                    if (!string.IsNullOrEmpty(path))
                        results.Add(path);
                }

                return results;
            }

            var independentGraphs = graphs.Where(x => !IsGraphContained(x.Key, graphs));

            // idempotently filter out duplicates - same projectID can show up multiple times for a multi-target build
            var finalResultSet = new Dictionary<string, ICollection<string>>();
            foreach (var r in independentGraphs)
            {
                var projectPath = GetProjectFilePath(r.Key);
                if (string.IsNullOrEmpty(projectPath))
                    continue;

                /*
                 * BUGFIX for https://github.com/petabridge/Incrementalist/issues/63
                 *
                 */
                if (finalResultSet.ContainsKey(projectPath))
                {
                    var existingPaths = finalResultSet[projectPath];
                    var newPaths = PrepareProjectPaths(r.Key, r.Value);
                    finalResultSet[projectPath] = existingPaths.Concat(newPaths)
                        .Where(p => !string.IsNullOrEmpty(p))  // Filter out any null paths
                        .Distinct()
                        .ToList();
                }
                else
                {
                    finalResultSet[projectPath] = PrepareProjectPaths(r.Key, r.Value);
                }
            }                

            return finalResultSet;
        }
    }
}
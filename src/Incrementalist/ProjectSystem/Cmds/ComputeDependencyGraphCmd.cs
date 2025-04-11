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
using Microsoft.VisualStudio.SolutionPersistence.Model;

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
        private readonly SolutionDetails _solution;

        public ComputeDependencyGraphCmd(ILogger logger, CancellationToken cancellationToken, SolutionDetails solution) : base(
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
             * We have to gather each unique project file separately in this case.
             */
            var additionalProjectIds = new List<Guid>();
            if (affectedSlnFiles.Any(x => x.Value.FileType == FileType.Project))
            {
                foreach (var proj in affectedSlnFiles.Where(x => x.Value.FileType == FileType.Project))
                    additionalProjectIds.AddRange(_solution.SolutionModel.SolutionProjects
                        .Where(x => x.FilePath.Equals(proj.Key)).Select(x => x.Id));
            }

            // Special case: if the solution itself is modified, return all projects
            if (_solution.SolutionFilePath != null && affectedSlnFiles.ContainsKey(_solution.SolutionFilePath))
            {
                return new Dictionary<string, ICollection<string>>()
                {
                    {
                        _solution.SolutionFilePath,
                        _solution.SolutionModel.SolutionProjects.Select(x => x.FilePath).ToList()
                    }
                };
            }

            var uniqueProjectIds = affectedSlnFiles.Select(x => (Guid)x.Value.ProjectId!).Concat(additionalProjectIds)
                .Distinct().ToList();
            
            
            
            
            var graphs = uniqueProjectIds.ToDictionary(x => x,
                GetProjectsThatTransitivelyDependOnThisProject);

            /*
             * Next: check to see if there are overlapping graphs and remove those from the final set
             */
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
            
            HashSet<Guid> GetProjectsThatTransitivelyDependOnThisProject(Guid projectId)
            {
                var projects = new HashSet<Guid>();

                var proj = _solution.SolutionModel.SolutionProjects.SingleOrDefault(c => c.Id == projectId);
                
                if(proj == null)
                    throw new ArgumentOutOfRangeException(nameof(projectId), $"Project {projectId} not found");
                
                // add ourselves
                projects.Add(proj.Id);
                
                // if we have no dependencies, we are done
                if (proj.Dependencies == null || proj.Dependencies.Count == 0)
                    return projects;
                
                var otherDeps = proj.Dependencies;
                
                foreach (var project in otherDeps)
                {
                    RecursivelyAddDeps(project);
                }

                return projects;

                // This is a directed acyclic graph, so we aren't going to have any cycles
                // therefore recursion is safe
                void RecursivelyAddDeps(SolutionProjectModel current)
                {
                    if (current.Dependencies == null || current.Dependencies.Count == 0)
                        return;

                    foreach (var dep in current.Dependencies)
                    {
                        if (!projects.Add(dep.Id))
                            continue;

                        RecursivelyAddDeps(dep);
                    }
                }
            }
            
            bool IsGraphContained(Guid root, Dictionary<Guid, HashSet<Guid>> otherGraphs)
            {
                return otherGraphs.Where(x => !x.Key.Equals(root))
                    .Any(nonRootGraph => nonRootGraph.Value.Contains(root));
            }

            ICollection<string> PrepareProjectPaths(Guid root, IEnumerable<Guid> graph)
            {
                var rootProject = _solution.SolutionModel.SolutionProjects.SingleOrDefault(c => c.Id == root);
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

            string? GetProjectFilePath(Guid project)
            {
                return _solution.SolutionModel.SolutionProjects.SingleOrDefault(c => c.Id == project)?.FilePath;
            }
        }
    }
}
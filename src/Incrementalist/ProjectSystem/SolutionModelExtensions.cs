using System;
using System.Collections.Generic;
using System.Linq;
using Incrementalist.ProjectSystem.Cmds;
using Microsoft.VisualStudio.SolutionPersistence.Model;
#nullable enable

namespace Incrementalist.ProjectSystem;

public static class SolutionModelExtensions
{
    public static ICollection<string> PrepareProjectPaths(this SolutionDetails solutionDetails, Guid root, IEnumerable<Guid> graph)
    {
        var rootProject = solutionDetails.SolutionModel.SolutionProjects.SingleOrDefault(c => c.Id == root);
        if (rootProject?.FilePath == null)
            return Array.Empty<string>();
                
        var results = new HashSet<string> { rootProject.FilePath };
        foreach (var p in graph)
        {
            var projectFilePath = solutionDetails.GetProjectFilePath(p);
            if (projectFilePath != null)
                results.Add(projectFilePath);
        }

        return results;
    }
    
    public static SolutionProjectModel? GetProject(this SolutionDetails solutionDetails, Guid project)
    {
        return solutionDetails.SolutionModel.SolutionProjects
            .SingleOrDefault(c => c.Id == project);
    }

    public static string? GetProjectFilePath(this SolutionDetails solutionDetails, Guid project)
    {
        return solutionDetails.SolutionModel.SolutionProjects
            .SingleOrDefault(c => c.Id == project)?.FilePath;
    }

    public static HashSet<Guid> GetProjectsThatDirectlyDependOnThisProject(this SolutionDetails solutionDetails,
        Guid projectId)
    {
        var projects = new HashSet<Guid>();

        var proj = solutionDetails.SolutionModel.SolutionProjects.SingleOrDefault(c => c.Id == projectId);
                
        if(proj == null)
            throw new ArgumentOutOfRangeException(nameof(projectId), $"Project {projectId} not found");
                
        // add ourselves
        projects.Add(proj.Id);
                
        // if we have no dependencies, we are done
        if (proj.Dependencies == null || proj.Dependencies.Count == 0)
            return projects;

        projects.UnionWith(proj.Dependencies.Select(c => c.Id));
        return projects;
    }

    public static HashSet<Guid> GetProjectsThatThisProjectDirectlyDependsOn(this SolutionDetails solutionDetails,
        Guid projectId)
    {
        var projects = new HashSet<Guid>();

        var proj = solutionDetails.GetProject(projectId);
                
        if(proj == null)
            throw new ArgumentOutOfRangeException(nameof(projectId), $"Project {projectId} not found");
                
        // need to iterate through the solution and find all projects that we depend on
        foreach(var project in solutionDetails.SolutionModel.SolutionProjects)
        {
            if (project.Dependencies == null || project.Dependencies.Count == 0)
                continue;

            foreach (var dep in project.Dependencies)
            {
                if (dep.Id == proj.Id)
                    projects.Add(project.Id);
            }
        }
        
        return projects;
    }
    
    public static HashSet<Guid> GetProjectsThatTransitivelyDependOnThisProject(this SolutionDetails solutionDetails, Guid projectId)
    {
        var projects = new HashSet<Guid>();

        var proj = solutionDetails.SolutionModel.SolutionProjects.SingleOrDefault(c => c.Id == projectId);
                
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
}
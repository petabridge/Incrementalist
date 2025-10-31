// -----------------------------------------------------------------------
// <copyright file="BuildEngine.Workspace.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.ProjectSystem;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd;

public sealed class WorkspaceBuildEngine(ILogger logger) : BuildEngine
{
    private readonly MSBuildWorkspace _msBuild = CreateWorkspace(logger);

    private static MSBuildWorkspace CreateWorkspace(ILogger logger)
    {
        var properties = new Dictionary<string, string>
        {
            // Required for SDK-style projects
            ["CheckForSystemRuntimeDependency"] = "true"
        };

        var workspace = MSBuildWorkspace.Create(properties);

        // Configure workspace to not skip unrecognized projects
        workspace.SkipUnrecognizedProjects = false;

        // Log any workspace loading issues
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            var message = $"Issue during workspace loading: {args.Diagnostic.Message}";
            var logLevel = args.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure
                ? LogLevel.Error
                : LogLevel.Warning;

            logger.Log(logLevel, message);
        });

        // Roslyn does not support FSharp projects, but .fsproj has same structure as .csproj files,
        // so can treat them as a known project type to support diff tracking
        workspace.AssociateFileExtensionWithLanguage("fsproj", Microsoft.CodeAnalysis.LanguageNames.CSharp);
        return workspace;
    }

    public override void Dispose() => _msBuild.Dispose();

    public override async Task<Solution> CreateSolutionAsync(AbsolutePath solutionFilePath, CancellationToken cancellationToken = default)
    {
        var progress = new Progress<ProjectLoadProgress>(x =>
        {
            logger.LogDebug("{Operation} project {Project} in {ElapsedTime}", x.Operation, x.FilePath, x.ElapsedTime);
        });
        var solution = await _msBuild.OpenSolutionAsync(solutionFilePath.Path, progress, cancellationToken);

        // Log loaded project count at debug level
        logger.LogDebug("Loaded {ProjectCount} projects from solution", solution.ProjectIds.Count);

        return new WorkspaceSolution(solution);
    }
}

public sealed class WorkspaceSolution : Solution
{
    private readonly Microsoft.CodeAnalysis.Solution _solution;

    public WorkspaceSolution(Microsoft.CodeAnalysis.Solution solution)
    {
        _solution = solution;
        FilePath = new AbsolutePath(solution.FilePath ?? throw new ArgumentException(nameof(solution.FilePath)));
        Projects = solution.Projects.Select(x => new WorkspaceProject(this, x)).ToList();
    }

    public override AbsolutePath FilePath { get; }
    public override IReadOnlyCollection<Project> Projects { get; }

    public override IReadOnlyCollection<Project> GetTransitiveProjects(Project project)
    {
        var innerProject = ((WorkspaceProject)project).Project;
        var dependencyGraph = _solution.GetProjectDependencyGraph();
        var projectIds = dependencyGraph.GetProjectsThatTransitivelyDependOnThisProject(innerProject.Id);
        var projects = projectIds.Select(x => Projects.Single(p => ((WorkspaceProject)p).Project.Id == x)).ToList();
        return projects;
    }
}

public sealed class WorkspaceProject : Project
{
    public WorkspaceProject(Solution solution, Microsoft.CodeAnalysis.Project project)
    {
        Project = project;
        FilePath = new AbsolutePath(project.FilePath ?? throw new ArgumentException(nameof(project.FilePath)));
        Solution = solution;
        Documents = project.Documents.Select(x => new WorkspaceDocument(this, x)).ToList();
    }

    public Microsoft.CodeAnalysis.Project Project { get; }
    public override AbsolutePath FilePath { get; }
    public override Solution Solution { get; }
    public override IReadOnlyCollection<Document> Documents { get; }
}

public sealed class WorkspaceDocument : Document
{
    public WorkspaceDocument(Project project, Microsoft.CodeAnalysis.Document document)
    {
        FilePath = new AbsolutePath(document.FilePath ?? throw new ArgumentException(nameof(document.FilePath)));
        Project = project;
        FileType = document.SourceCodeKind == Microsoft.CodeAnalysis.SourceCodeKind.Regular ? FileType.Code : FileType.Script;
    }

    public override AbsolutePath FilePath { get; }
    public override Project Project { get; }
    public override FileType FileType { get; }
}
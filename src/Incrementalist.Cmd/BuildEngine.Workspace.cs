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
#if NET10_0_OR_GREATER
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            var message = $"Issue during workspace loading: {args.Diagnostic.Message}";
            var logLevel = args.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure
                ? LogLevel.Error
                : LogLevel.Warning;

            logger.Log(logLevel, message);
        });
#else
        workspace.WorkspaceFailed += (_, args) =>
        {
            var message = $"Issue during workspace loading: {args.Diagnostic.Message}";
            var logLevel = args.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure
                ? LogLevel.Error
                : LogLevel.Warning;

            logger.Log(logLevel, message);
        };
#endif

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

        // Source documents plus AdditionalFiles (appsettings.json, analyzer configs, etc.).
        // AdditionalDocuments are not part of project.Documents, so they must be added explicitly
        // or changes to them are invisible to affected-project detection.
        Documents = project.Documents.Select(x => (Document)new WorkspaceDocument(this, x))
            .Concat(project.AdditionalDocuments.Select(x => new WorkspaceAdditionalDocument(this, x)))
            .ToList();
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

/// <summary>
///     A non-source file registered as an <c>AdditionalFiles</c> item, such as
///     <c>appsettings.json</c> or an analyzer configuration file.
/// </summary>
public sealed class WorkspaceAdditionalDocument : Document
{
    public WorkspaceAdditionalDocument(Project project, Microsoft.CodeAnalysis.TextDocument document)
    {
        FilePath = new AbsolutePath(document.FilePath ?? throw new ArgumentException(nameof(document.FilePath)));
        Project = project;
    }

    public override AbsolutePath FilePath { get; }
    public override Project Project { get; }
    public override FileType FileType => FileType.Other;
}
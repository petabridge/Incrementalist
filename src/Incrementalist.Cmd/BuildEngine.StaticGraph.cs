// -----------------------------------------------------------------------
// <copyright file="BuildEngine.StaticGraph.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.ProjectSystem;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd;

public sealed class StaticGraphBuildEngine : BuildEngine
{
    private readonly MicrosoftBuildEventListener _listener;

    public StaticGraphBuildEngine(ILogger logger, bool verbose)
    {
        _listener = new MicrosoftBuildEventListener(logger, verbose ? EventLevel.Informational : EventLevel.Warning);
    }

    public override void Dispose()
    {
        _listener.Dispose();
    }

    public override Task<Solution> CreateSolutionAsync(AbsolutePath solutionFilePath, CancellationToken cancellationToken = default)
    {
        var entryPoint = new ProjectGraphEntryPoint(solutionFilePath.Path);
        var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
        var degreeOfParallelism = Environment.ProcessorCount;
        var projectGraph = new ProjectGraph([entryPoint], projectCollection, projectInstanceFactory: null, degreeOfParallelism, cancellationToken);
        return Task.FromResult<Solution>(new StaticGraphSolution(solutionFilePath, projectGraph));
    }
}

public sealed class StaticGraphSolution : Solution
{
    private readonly ProjectGraph _projectGraph;

    public StaticGraphSolution(AbsolutePath filePath, ProjectGraph projectGraph)
    {
        _projectGraph = projectGraph;
        FilePath = filePath;
        // Deduplicate by FullPath - multi-targeted projects create separate nodes per target framework.
        // Group rather than DistinctBy so every node's items reach the project: a framework-conditional
        // AdditionalFiles entry exists on only one node, and dropping the others would lose it.
        Projects = projectGraph.ProjectNodesTopologicallySorted
            .GroupBy(x => x.ProjectInstance.FullPath)
            .Select(g => new StaticGraphProject(this, g.First(), g.ToList()))
            .ToList();
    }

    public override AbsolutePath FilePath { get; }
    public override IReadOnlyCollection<Project> Projects { get; }

    public override IReadOnlyCollection<Project> GetTransitiveProjects(Project project)
    {
        var result = new HashSet<Project>();
        // Iterate all nodes for this project (one per target framework)
        foreach (var node in _projectGraph.ProjectNodesTopologicallySorted.Where(x => x.ProjectInstance.FullPath == project.FilePath.Path))
        {
            foreach (var referencingNode in node.ReferencingProjects)
            {
                // Match by FullPath since Projects is deduplicated
                var proj = Projects.SingleOrDefault(p => p.FilePath.Path == referencingNode.ProjectInstance.FullPath);
                if (proj != null)
                    result.Add(proj);
            }
        }
        return result.ToList();
    }
}

public sealed class StaticGraphProject : Project
{
    public StaticGraphProject(Solution solution, ProjectGraphNode node)
        : this(solution, node, [node])
    {
    }

    /// <summary>
    ///     Creates a project from every graph node that shares this project's path.
    /// </summary>
    /// <remarks>
    ///     A multi-targeted project produces one node per target framework, and each node can carry
    ///     different items — an <c>AdditionalFiles</c> entry guarded by a target-framework condition
    ///     exists on only one node. Collecting documents from a single node would silently drop the
    ///     others, so items are unioned across all nodes and deduplicated by path.
    /// </remarks>
    public StaticGraphProject(Solution solution, ProjectGraphNode node, IReadOnlyCollection<ProjectGraphNode> nodes)
    {
        Node = node;
        FilePath = new AbsolutePath(node.ProjectInstance.FullPath);
        Solution = solution;

        // Compile items are source files; AdditionalFiles are non-source inputs such as
        // appsettings.json or analyzer configs. Both must be tracked or changes to the latter
        // are invisible to affected-project detection.
        var documents = nodes
            .SelectMany(n => n.ProjectInstance.Items)
            .Where(x => x.ItemType is "Compile" or "AdditionalFiles")
            .Select(x => (Document)(x.ItemType == "Compile"
                ? new StaticGraphDocument(this, x)
                : new StaticGraphAdditionalDocument(this, x)))
            .DistinctBy(d => d.FilePath.Path)
            .ToList();

        Documents = documents;
    }

    public ProjectGraphNode Node { get; }
    public override AbsolutePath FilePath { get; }
    public override Solution Solution { get; }
    public override IReadOnlyCollection<Document> Documents { get; }
}

public sealed class StaticGraphDocument : Document
{
    public StaticGraphDocument(StaticGraphProject project, ProjectItemInstance projectItemInstance)
    {
        var fullPath = projectItemInstance.GetMetadataValue("FullPath");
        FilePath = new AbsolutePath(fullPath);
        Project = project;
        FileType = FileType.Code;
    }

    public override AbsolutePath FilePath { get; }
    public override Project Project { get; }
    public override FileType FileType { get; }
}

/// <summary>
///     A non-source file registered as an <c>AdditionalFiles</c> item, such as
///     <c>appsettings.json</c> or an analyzer configuration file.
/// </summary>
public sealed class StaticGraphAdditionalDocument : Document
{
    public StaticGraphAdditionalDocument(StaticGraphProject project, ProjectItemInstance projectItemInstance)
    {
        var fullPath = projectItemInstance.GetMetadataValue("FullPath");
        FilePath = new AbsolutePath(fullPath);
        Project = project;
    }

    public override AbsolutePath FilePath { get; }
    public override Project Project { get; }
    public override FileType FileType => FileType.Other;
}
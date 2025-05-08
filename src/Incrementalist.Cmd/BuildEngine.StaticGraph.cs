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

    public override Task<Solution> CreateSolutionAsync(string solutionFilePath, CancellationToken cancellationToken = default)
    {
        var entryPoint = new ProjectGraphEntryPoint(solutionFilePath);
        var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
        var degreeOfParallelism = Environment.ProcessorCount;
        var projectGraph = new ProjectGraph([entryPoint], projectCollection, projectInstanceFactory: null, degreeOfParallelism, cancellationToken);
        return Task.FromResult<Solution>(new StaticGraphSolution(new AbsolutePath(solutionFilePath), projectGraph));
    }
}

public sealed class StaticGraphSolution : Solution
{
    private readonly ProjectGraph _projectGraph;

    public StaticGraphSolution(AbsolutePath filePath, ProjectGraph projectGraph)
    {
        _projectGraph = projectGraph;
        FilePath = filePath;
        Projects = projectGraph.ProjectNodesTopologicallySorted.Select(x => new StaticGraphProject(this, x)).ToList();
    }

    public override AbsolutePath FilePath { get; }
    public override IReadOnlyCollection<Project> Projects { get; }

    public override IReadOnlyCollection<Project> GetTransitiveProjects(Project project)
    {
        var result = new List<Project>();
        foreach (var node in _projectGraph.ProjectNodesTopologicallySorted.Where(x => x.ProjectInstance.FullPath == project.FilePath.Path))
        {
            result.AddRange(node.ReferencingProjects.Select(x => Projects.Single(p => ((StaticGraphProject)p).Node == x)));
        }
        return result;
    }
}

public sealed class StaticGraphProject : Project
{
    public StaticGraphProject(Solution solution, ProjectGraphNode node)
    {
        Node = node;
        FilePath = new AbsolutePath(node.ProjectInstance.FullPath);
        Solution = solution;
        var documents = node.ProjectInstance.Items.Where(x => x.ItemType == "Compile").Select(x => new StaticGraphDocument(this, x)).ToList();
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
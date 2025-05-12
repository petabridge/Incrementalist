// -----------------------------------------------------------------------
// <copyright file="InMemoryBuildEngine.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.ProjectSystem;

namespace Incrementalist.Tests;

internal class InMemoryBuildEngine(InMemorySolution solution) : BuildEngine
{
    public override Task<Solution> CreateSolutionAsync(AbsolutePath solutionFilePath, CancellationToken cancellationToken = default)
    {
        solution.SetFilePath(solutionFilePath);
        return Task.FromResult<Solution>(solution);
    }

    public override void Dispose()
    {
    }
}

internal class InMemorySolution(IReadOnlyCollection<Project> projects) : Solution
{
    private AbsolutePath _filePath = new(Environment.CurrentDirectory);

    internal void SetFilePath(AbsolutePath path) => _filePath = path;
    public override AbsolutePath FilePath => _filePath;
    public override IReadOnlyCollection<Project> Projects { get; } = projects;
    public override IReadOnlyCollection<Project> GetTransitiveProjects(Project project) => throw new NotSupportedException();
}

internal class InMemoryProject(AbsolutePath filePath) : Project
{
    public override AbsolutePath FilePath { get; } = filePath;
    public override Solution Solution => throw new NotSupportedException();
    public override IReadOnlyCollection<Document> Documents => throw new NotSupportedException();
}

// -----------------------------------------------------------------------
// <copyright file="ISolution.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Incrementalist.ProjectSystem;

public abstract class BuildEngine : IDisposable
{
    public abstract Task<Solution> CreateSolutionAsync(string solutionFilePath, CancellationToken cancellationToken = default);
    public abstract void Dispose();
}

public abstract class Item
{
    public abstract AbsolutePath FilePath { get; }
}

public abstract class Solution : Item
{
    public abstract IReadOnlyCollection<Project> Projects { get; }
    public abstract IReadOnlyCollection<Project> GetTransitiveProjects(Project project);
}

public abstract class Project : Item
{
    public abstract Solution Solution { get; }
    public abstract IReadOnlyCollection<Document> Documents { get; }
}

public abstract class Document : Item
{
    public abstract Project Project { get; }
    public abstract FileType FileType { get; }
}

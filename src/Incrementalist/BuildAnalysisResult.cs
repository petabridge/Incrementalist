// -----------------------------------------------------------------------
// <copyright file="BuildAnalysisResult.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Incrementalist
{
    /// <summary>
    /// Base class for representing the result of analyzing which parts of the solution need to be built.
    /// </summary>
    public abstract class BuildAnalysisResult
    {
    }

    /// <summary>
    /// Represents a result where only specific projects need to be built.
    /// </summary>
    public sealed class IncrementalBuildResult : BuildAnalysisResult
    {
        public IncrementalBuildResult(IReadOnlyList<AbsolutePath> affectedProjects)
        {
            AffectedProjects = affectedProjects ?? throw new ArgumentNullException(nameof(affectedProjects));
        }

        /// <summary>
        /// The list of projects that need to be built.
        /// </summary>
        public IReadOnlyList<AbsolutePath> AffectedProjects { get; }
    }

    /// <summary>
    /// Represents a result where the entire solution needs to be built.
    /// </summary>
    public sealed class FullSolutionBuildResult : BuildAnalysisResult
    {
        public FullSolutionBuildResult(AbsolutePath solutionPath)
        {
            SolutionPath = solutionPath ?? throw new ArgumentNullException(nameof(solutionPath));
        }

        /// <summary>
        /// The path to the solution file that needs to be built.
        /// </summary>
        public AbsolutePath SolutionPath { get; }
    }
}
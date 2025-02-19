// -----------------------------------------------------------------------
// <copyright file="BuildAnalysisResultTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Xunit;

namespace Incrementalist.Tests
{
    public class BuildAnalysisResultTests
    {
        [Fact]
        public void IncrementalBuildResult_Constructor_ValidatesInput()
        {
            var projects = new[] { "Project1.csproj", "Project2.csproj" };
            var result = new IncrementalBuildResult(projects);
            Assert.Equal(projects, result.AffectedProjects);
        }

        [Fact]
        public void IncrementalBuildResult_Constructor_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new IncrementalBuildResult(null));
        }

        [Fact]
        public void FullSolutionBuildResult_Constructor_ValidatesInput()
        {
            var solutionPath = "MySolution.sln";
            var result = new FullSolutionBuildResult(solutionPath);
            Assert.Equal(solutionPath, result.SolutionPath);
        }

        [Fact]
        public void FullSolutionBuildResult_Constructor_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new FullSolutionBuildResult(null));
        }
    }
} 
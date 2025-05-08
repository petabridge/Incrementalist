// -----------------------------------------------------------------------
// <copyright file="BuildAnalysisResultTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Incrementalist.Cmd;
using Incrementalist.ProjectSystem;
using Incrementalist.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Incrementalist.Tests
{
    public class BuildAnalysisResultTests
    {
        private readonly WorkspaceBuildEngine _engine = new(NullLogger.Instance);

        [Fact]
        public void IncrementalBuildResult_Constructor_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new IncrementalBuildResult(null!));
        }

        [Fact]
        public void FullSolutionBuildResult_Constructor_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new FullSolutionBuildResult(null!));
        }

        private AbsolutePath MakeAbsolutePath(string path)
        {
            return new AbsolutePath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private static readonly string[] sourceArray = new[] { "Project1.csproj", "Project2.csproj" };

        [Fact]
        public async Task CreateBuildResult_AllProjectsAffected_ReturnsFullSolutionBuildResult()
        {
            // Arrange
            var sln = ProjectSampleGenerator.CreateSolutionFile("test.sln", ["Project1", "Project2"]);
            var solution = await _engine.CreateSolutionAsync(sln.FullName);

            var affectedProjects = sourceArray.Select(MakeAbsolutePath)
                .ToList();

            // Act
            var result = SolutionWideChangeDetector.CreateBuildResult(solution, affectedProjects);

            // Assert
            Assert.IsType<FullSolutionBuildResult>(result);
            var fullResult = (FullSolutionBuildResult)result;
            Assert.Equal(sln.FullName, fullResult.SolutionPath.Path);
        }

        [Fact]
        public async Task CreateBuildResult_SomeProjectsAffected_ReturnsIncrementalBuildResult()
        {
            // Arrange
            var sln = ProjectSampleGenerator.CreateSolutionFile("test.sln", ["Project1", "Project2", "Project3"]);
            var solution = await _engine.CreateSolutionAsync(sln.FullName);

            var affectedProjects = new[] { "Project1.csproj", "Project2.csproj" }.Select(MakeAbsolutePath)
                .ToList(); // Only 2 of 3 projects affected

            // Act
            var result = SolutionWideChangeDetector.CreateBuildResult(solution, affectedProjects);

            // Assert
            Assert.IsType<IncrementalBuildResult>(result);
            var incrementalResult = (IncrementalBuildResult)result;
            Assert.Equal(affectedProjects, incrementalResult.AffectedProjects);
        }

        [Fact]
        public void CreateBuildResult_NullAffectedProjects_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SolutionWideChangeDetector.CreateBuildResult(new NullSolution(), null!));
        }

        private class NullSolution : Solution
        {
            public override AbsolutePath FilePath => throw new NotSupportedException();
            public override IReadOnlyCollection<Project> Projects => throw new NotSupportedException();
            public override IReadOnlyCollection<Project> GetTransitiveProjects(Project project) => throw new NotSupportedException();
        }
    }
}
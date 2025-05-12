// -----------------------------------------------------------------------
// <copyright file="BuildAnalysisResultTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Incrementalist.Tests
{
    public class BuildAnalysisResultTests
    {
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

        private static AbsolutePath MakeAbsolutePath(string path)
        {
            return new AbsolutePath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private static readonly string[] sourceArray = new[] { "Project1.csproj", "Project2.csproj" };

        [Fact]
        public async Task CreateBuildResult_AllProjectsAffected_ReturnsFullSolutionBuildResult()
        {
            // Arrange
            InMemoryProject[] projects = [new(MakeAbsolutePath("Project1.csproj")), new(MakeAbsolutePath("Project2.csproj"))];
            var engine = new InMemoryBuildEngine(new InMemorySolution(projects));
            var solution = await engine.CreateSolutionAsync(MakeAbsolutePath("test.sln"));

            var affectedProjects = sourceArray.Select(MakeAbsolutePath)
                .ToList();

            // Act
            var result = SolutionWideChangeDetector.CreateBuildResult(solution, affectedProjects);

            // Assert
            Assert.IsType<FullSolutionBuildResult>(result);
            var fullResult = (FullSolutionBuildResult)result;
            Assert.Equal(solution.FilePath, fullResult.SolutionPath);
        }

        [Fact]
        public async Task CreateBuildResult_SomeProjectsAffected_ReturnsIncrementalBuildResult()
        {
            // Arrange
            InMemoryProject[] projects = [new(MakeAbsolutePath("Project1.csproj")), new(MakeAbsolutePath("Project2.csproj")), new(MakeAbsolutePath("Project3.csproj"))];
            var engine = new InMemoryBuildEngine(new InMemorySolution(projects));
            var solution = await engine.CreateSolutionAsync(MakeAbsolutePath("test.sln"));

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
            Assert.Throws<ArgumentNullException>(() => SolutionWideChangeDetector.CreateBuildResult(new InMemorySolution([]), null!));
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="BuildAnalysisResultTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
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
        
        private AbsolutePath MakeAbsolutePath(string path)
        {
            return new AbsolutePath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private static readonly string[] sourceArray = new[] { "Project1.csproj", "Project2.csproj" };

        [Fact]
        public void CreateBuildResult_AllProjectsAffected_ReturnsFullSolutionBuildResult()
        {
            // Arrange
            var solutionPath = MakeAbsolutePath("test.sln");
            var workspace = new AdhocWorkspace();
            var solutionInfo = SolutionInfo.Create(
                SolutionId.CreateNewId(),
                VersionStamp.Create(),
                solutionPath.Path);
            
            var solution = workspace.AddSolution(solutionInfo);

            var projectIds = new[] { ProjectId.CreateNewId(), ProjectId.CreateNewId() };
            foreach (var id in projectIds)
            {
                var projectInfo = ProjectInfo.Create(
                    id,
                    VersionStamp.Create(),
                    $"Project{id.Id}",
                    $"Project{id.Id}",
                    LanguageNames.CSharp,
                    filePath: $"Project{id.Id}.csproj");
                solution = solution.AddProject(projectInfo);
            }

            var affectedProjects = sourceArray.Select(MakeAbsolutePath)
                .ToList();

            // Act
            var result = SolutionWideChangeDetector.CreateBuildResult(solution, affectedProjects);

            // Assert
            Assert.IsType<FullSolutionBuildResult>(result);
            var fullResult = (FullSolutionBuildResult)result;
            Assert.Equal(solutionPath, fullResult.SolutionPath);
        }

        [Fact]
        public void CreateBuildResult_SomeProjectsAffected_ReturnsIncrementalBuildResult()
        {
            // Arrange
            var solutionPath = MakeAbsolutePath("test.sln");
            var workspace = new AdhocWorkspace();
            var solutionInfo = SolutionInfo.Create(
                SolutionId.CreateNewId(),
                VersionStamp.Create(),
                solutionPath.Path);
            
            var solution = workspace.AddSolution(solutionInfo);

            var projectIds = new[] { ProjectId.CreateNewId(), ProjectId.CreateNewId(), ProjectId.CreateNewId() };
            foreach (var id in projectIds)
            {
                var projectInfo = ProjectInfo.Create(
                    id,
                    VersionStamp.Create(),
                    $"Project{id.Id}",
                    $"Project{id.Id}",
                    LanguageNames.CSharp,
                    filePath: $"Project{id.Id}.csproj");
                solution = solution.AddProject(projectInfo);
            }

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
            var workspace = new AdhocWorkspace();
            var solution = workspace.AddSolution(SolutionInfo.Create(
                SolutionId.CreateNewId(),
                VersionStamp.Create(),
                "test.sln"));
            Assert.Throws<ArgumentNullException>(() => SolutionWideChangeDetector.CreateBuildResult(solution, null!));
        }
    }
} 
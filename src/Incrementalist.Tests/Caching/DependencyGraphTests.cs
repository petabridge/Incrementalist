#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Incrementalist.Caching;
using Incrementalist.ProjectSystem;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Incrementalist.Tests.Caching
{
    public class DependencyGraphTests
    {
        [Fact]
        public void GetProjectsToRebuild_WithNoAffectedFiles_ReturnsEmptyDictionary()
        {
            // Arrange
            var cache = new DependencyCache(
                Version: DependencyCacheIO.CurrentVersion,
                SolutionPath: "test.sln",
                Checksum: "test-checksum",
                Projects: ImmutableDictionary<string, ProjectNode>.Empty);

            // Act
            var result = cache.GetProjectsToRebuild(new Dictionary<string, SlnFile>());

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetProjectsToRebuild_WithDirectlyAffectedProject_ReturnsProjectAndDependents()
        {
            // Arrange
            var projects = new Dictionary<string, ProjectNode>
            {
                ["Project1.csproj"] = new("Project1.csproj", ProjectId.CreateNewId(), ImmutableList<string>.Empty),
                ["Project2.csproj"] = new("Project2.csproj", ProjectId.CreateNewId(), ImmutableList<string>.Empty)
            }.ToImmutableDictionary();

            var cache = new DependencyCache(
                Version: DependencyCacheIO.CurrentVersion,
                SolutionPath: "test.sln",
                Checksum: "test-checksum",
                Projects: projects);

            var affectedFiles = new Dictionary<string, SlnFile>
            {
                ["Project1.csproj"] = new(FileType.Project, null)
            };

            // Act
            var result = cache.GetProjectsToRebuild(affectedFiles);

            // Assert
            Assert.Single(result);
            Assert.True(result.ContainsKey("Project1.csproj"));
            Assert.Single(result["Project1.csproj"]);
            Assert.Contains("Project1.csproj", result["Project1.csproj"]);
        }

        [Fact]
        public void GetProjectsToRebuild_WithDependentProjects_ReturnsAllAffectedProjects()
        {
            // Arrange
            var projects = new Dictionary<string, ProjectNode>
            {
                ["Project1.csproj"] = new("Project1.csproj", ProjectId.CreateNewId(), ImmutableList<string>.Empty),
                ["Project2.csproj"] = new("Project2.csproj", ProjectId.CreateNewId(), ImmutableList.Create("Project1.csproj")),
                ["Project3.csproj"] = new("Project3.csproj", ProjectId.CreateNewId(), ImmutableList.Create("Project2.csproj"))
            }.ToImmutableDictionary();

            var cache = new DependencyCache(
                Version: DependencyCacheIO.CurrentVersion,
                SolutionPath: "test.sln",
                Checksum: "test-checksum",
                Projects: projects);

            var affectedFiles = new Dictionary<string, SlnFile>
            {
                ["Project1.csproj"] = new(FileType.Project, null)
            };

            // Act
            var result = cache.GetProjectsToRebuild(affectedFiles);

            // Assert
            Assert.Single(result);
            Assert.True(result.ContainsKey("Project1.csproj"));
            Assert.Equal(3, result["Project1.csproj"].Count);
            Assert.Contains("Project1.csproj", result["Project1.csproj"]);
            Assert.Contains("Project2.csproj", result["Project1.csproj"]);
            Assert.Contains("Project3.csproj", result["Project1.csproj"]);
        }

        [Fact]
        public void GetProjectsToRebuild_WithMultipleAffectedProjects_ReturnsAllDependentProjects()
        {
            // Arrange
            var projects = new Dictionary<string, ProjectNode>
            {
                ["Project1.csproj"] = new("Project1.csproj", ProjectId.CreateNewId(), ImmutableList<string>.Empty),
                ["Project2.csproj"] = new("Project2.csproj", ProjectId.CreateNewId(), ImmutableList<string>.Empty),
                ["Project3.csproj"] = new("Project3.csproj", ProjectId.CreateNewId(), ImmutableList.Create("Project1.csproj")),
                ["Project4.csproj"] = new("Project4.csproj", ProjectId.CreateNewId(), ImmutableList.Create("Project2.csproj")),
                ["Project5.csproj"] = new("Project5.csproj", ProjectId.CreateNewId(), ImmutableList.Create("Project3.csproj", "Project4.csproj"))
            }.ToImmutableDictionary();

            var cache = new DependencyCache(
                Version: DependencyCacheIO.CurrentVersion,
                SolutionPath: "test.sln",
                Checksum: "test-checksum",
                Projects: projects);

            var affectedFiles = new Dictionary<string, SlnFile>
            {
                ["Project1.csproj"] = new(FileType.Project, null),
                ["Project2.csproj"] = new(FileType.Project, null)
            };

            // Act
            var result = cache.GetProjectsToRebuild(affectedFiles);

            // Assert
            Assert.Equal(2, result.Count);
            
            Assert.True(result.ContainsKey("Project1.csproj"));
            var project1Dependents = result["Project1.csproj"];
            Assert.Equal(3, project1Dependents.Count);
            Assert.Contains("Project1.csproj", project1Dependents);
            Assert.Contains("Project3.csproj", project1Dependents);
            Assert.Contains("Project5.csproj", project1Dependents);

            Assert.True(result.ContainsKey("Project2.csproj"));
            var project2Dependents = result["Project2.csproj"];
            Assert.Equal(3, project2Dependents.Count);
            Assert.Contains("Project2.csproj", project2Dependents);
            Assert.Contains("Project4.csproj", project2Dependents);
            Assert.Contains("Project5.csproj", project2Dependents);
        }

        [Fact]
        public void GetProjectsToRebuild_WithNonProjectFiles_IgnoresNonProjectFiles()
        {
            // Arrange
            var projects = new Dictionary<string, ProjectNode>
            {
                ["Project1.csproj"] = new("Project1.csproj", ProjectId.CreateNewId(), ImmutableList<string>.Empty),
                ["Project2.csproj"] = new("Project2.csproj", ProjectId.CreateNewId(), ImmutableList.Create("Project1.csproj"))
            }.ToImmutableDictionary();

            var cache = new DependencyCache(
                Version: DependencyCacheIO.CurrentVersion,
                SolutionPath: "test.sln",
                Checksum: "test-checksum",
                Projects: projects);

            var affectedFiles = new Dictionary<string, SlnFile>
            {
                ["Project1.csproj"] = new(FileType.Project, null),
                ["SomeFile.cs"] = new(FileType.Code, null),
                ["AnotherFile.cs"] = new(FileType.Code, null)
            };

            // Act
            var result = cache.GetProjectsToRebuild(affectedFiles);

            // Assert
            Assert.Single(result);
            Assert.True(result.ContainsKey("Project1.csproj"));
            Assert.Equal(2, result["Project1.csproj"].Count);
            Assert.Contains("Project1.csproj", result["Project1.csproj"]);
            Assert.Contains("Project2.csproj", result["Project1.csproj"]);
        }

        [Fact]
        public void GetProjectsToRebuild_WithProjectFiles_RebuildsContainingProjects()
        {
            // Arrange
            var projects = new Dictionary<string, ProjectNode>
            {
                ["Project1.csproj"] = new("Project1.csproj", ProjectId.CreateNewId(), ImmutableList<string>.Empty),
                ["Project2.csproj"] = new("Project2.csproj", ProjectId.CreateNewId(), ImmutableList.Create("Project1.csproj"))
            }.ToImmutableDictionary();

            var cache = new DependencyCache(
                Version: DependencyCacheIO.CurrentVersion,
                SolutionPath: "test.sln",
                Checksum: "test-checksum",
                Projects: projects);

            var affectedFiles = new Dictionary<string, SlnFile>
            {
                ["Project1.csproj"] = new SlnFile(FileType.Project, null),
                ["Project2.csproj"] = new SlnFile(FileType.Project, null)
            };

            // Act
            var result = cache.GetProjectsToRebuild(affectedFiles);

            // Assert
            Assert.Equal(2, result.Count);
            
            Assert.True(result.ContainsKey("Project1.csproj"));
            Assert.Equal(2, result["Project1.csproj"].Count);
            Assert.Contains("Project1.csproj", result["Project1.csproj"]);
            Assert.Contains("Project2.csproj", result["Project1.csproj"]);

            Assert.True(result.ContainsKey("Project2.csproj"));
            Assert.Single(result["Project2.csproj"]);
            Assert.Contains("Project2.csproj", result["Project2.csproj"]);
        }

        [Fact]
        public void GetProjectsToRebuild_WithSourceFiles_ReturnsProjectsAndDependents()
        {
            // Arrange
            var project1Id = ProjectId.CreateNewId();
            var project2Id = ProjectId.CreateNewId();

            var projects = new Dictionary<string, ProjectNode>
            {
                ["Project1.csproj"] = new("Project1.csproj", project1Id, ImmutableList<string>.Empty),
                ["Project2.csproj"] = new("Project2.csproj", project2Id, ImmutableList.Create("Project1.csproj"))
            }.ToImmutableDictionary();

            var cache = new DependencyCache(
                Version: DependencyCacheIO.CurrentVersion,
                SolutionPath: "test.sln",
                Checksum: "test-checksum",
                Projects: projects);

            var affectedFiles = new Dictionary<string, SlnFile>
            {
                ["Project1/Class1.cs"] = new SlnFile(FileType.Code, project1Id),
                ["Project1/Class2.cs"] = new SlnFile(FileType.Code, project1Id),
                ["Project2/Class3.cs"] = new SlnFile(FileType.Code, project2Id)
            };

            // Act
            var result = cache.GetProjectsToRebuild(affectedFiles);

            // Assert
            Assert.Equal(2, result.Count);
            
            Assert.True(result.ContainsKey("Project1.csproj"));
            Assert.Equal(2, result["Project1.csproj"].Count);
            Assert.Contains("Project1.csproj", result["Project1.csproj"]);
            Assert.Contains("Project2.csproj", result["Project1.csproj"]);

            Assert.True(result.ContainsKey("Project2.csproj"));
            Assert.Single(result["Project2.csproj"]);
            Assert.Contains("Project2.csproj", result["Project2.csproj"]);
        }
    }
} 
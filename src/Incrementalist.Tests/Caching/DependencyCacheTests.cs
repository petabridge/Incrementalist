// -----------------------------------------------------------------------
// <copyright file="DependencyCacheTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Incrementalist.Caching;
using Incrementalist.Git;
using Incrementalist.ProjectSystem;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Caching
{
    [Collection(MSBuildCollectionFixture.Name)]
    public class DependencyCacheTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly DisposableRepository _repository;
        private readonly MSBuildWorkspace _workspace;
        
        public DependencyCacheTests(ITestOutputHelper output, MSBuildFixture fixture)
        {
            _output = output;
            _repository = new DisposableRepository();
            _workspace = fixture.Workspace;
        }
        
        public void Dispose()
        {
            _repository?.Dispose();
        }
        
        [Fact]
        public async Task SaveAndLoad_PreservesAllData()
        {
            // Arrange
            var projects = new Dictionary<string, ProjectNode>
            {
                ["src/Project1/Project1.csproj"] = new(
                    "src/Project1/Project1.csproj",
                    ProjectId.CreateNewId(),
                    ImmutableList<string>.Empty),
                    
                ["src/Project2/Project2.csproj"] = new(
                    "src/Project2/Project2.csproj",
                    ProjectId.CreateNewId(),
                    ImmutableList.Create("src/Project1/Project1.csproj")),
                    
                ["tests/Project1.Tests/Project1.Tests.csproj"] = new(
                    "tests/Project1.Tests/Project1.Tests.csproj",
                    ProjectId.CreateNewId(),
                    ImmutableList.Create("src/Project1/Project1.csproj"))
            };
            
            var cache = new DependencyCache(
                DependencyCacheIO.CurrentVersion,
                "src/MySolution.sln",
                "sample-checksum",
                projects.ToImmutableDictionary());
                
            var cachePath = Path.Combine(_repository.BasePath, ".incrementalist", "dependency-graph.cache.json");
            
            // Act
            await DependencyCacheIO.SaveAsync(cachePath, cache);
            var loaded = await DependencyCacheIO.LoadAsync(cachePath);
            
            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(cache.Version, loaded.Version);
            Assert.Equal(cache.SolutionPath, loaded.SolutionPath);
            Assert.Equal(cache.Checksum, loaded.Checksum);
            Assert.Equal(cache.Projects.Count, loaded.Projects.Count);
            
            foreach (var (path, node) in cache.Projects)
            {
                Assert.True(loaded.Projects.ContainsKey(path));
                var loadedNode = loaded.Projects[path];
                Assert.Equal(node.Path, loadedNode.Path);
                Assert.Equal(node.Dependencies, loadedNode.Dependencies);
            }
        }
        
        [Fact]
        public async Task Load_WithMissingFile_ReturnsNull()
        {
            // Arrange
            var cachePath = Path.Combine(_repository.BasePath, ".incrementalist", "dependency-graph.cache.json");
            
            // Act
            var loaded = await DependencyCacheIO.LoadAsync(cachePath);
            
            // Assert
            Assert.Null(loaded);
        }

        [Fact]
        public async Task Save_WithNullCache_ThrowsArgumentNullException()
        {
            // Arrange
            var cachePath = Path.Combine(_repository.BasePath, ".incrementalist", "dependency-graph.cache.json");
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => DependencyCacheIO.SaveAsync(cachePath, null!));
        }

        [Fact]
        public async Task Save_WithNullPath_ThrowsArgumentNullException()
        {
            // Arrange
            var cache = new DependencyCache(
                DependencyCacheIO.CurrentVersion,
                "src/MySolution.sln",
                "sample-checksum",
                ImmutableDictionary<string, ProjectNode>.Empty);
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => DependencyCacheIO.SaveAsync(null!, cache));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Save_WithEmptyPath_ThrowsArgumentException(string path)
        {
            // Arrange
            var cache = new DependencyCache(
                DependencyCacheIO.CurrentVersion,
                "src/MySolution.sln",
                "sample-checksum",
                ImmutableDictionary<string, ProjectNode>.Empty);
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => DependencyCacheIO.SaveAsync(path, cache));
        }

        [Fact]
        public async Task GetProjectsToRebuild_MatchesLiveDependencyGraph()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");
            var project3Path = Path.Combine(_repository.BasePath, "src", "Project3", "Project3.csproj");

            // Create project directories
            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project3Path)!);

            // Create source files for Project1
            var project1SrcDir = Path.Combine(Path.GetDirectoryName(project1Path)!, "src");
            Directory.CreateDirectory(project1SrcDir);
            await File.WriteAllTextAsync(Path.Combine(project1SrcDir, "Class1.cs"), "namespace Project1 { public class Class1 {} }");
            await File.WriteAllTextAsync(Path.Combine(project1SrcDir, "Class2.cs"), "namespace Project1 { public class Class2 {} }");

            // Create solution and project files
            await File.WriteAllTextAsync(solutionPath, ProjectSampleGenerator.CreateSolutionFile("test", new[] { "Project1", "Project2", "Project3" }));
            await File.WriteAllTextAsync(project1Path, ProjectSampleGenerator.CreateProjectFile("Project1"));
            await File.WriteAllTextAsync(project2Path, ProjectSampleGenerator.CreateProjectFile("Project2", new[] { "Project1" }));
            await File.WriteAllTextAsync(project3Path, ProjectSampleGenerator.CreateProjectFile("Project3", new[] { "Project2" }));

            // Load solution
            var solution = await _workspace.OpenSolutionAsync(solutionPath);

            // Create cache
            var cache = await DependencyCacheHelper.CreateFromSolutionAsync(solution);

            // Create affected files (source files from Project1)
            var project1Id = solution.Projects.First(p => p.FilePath == project1Path).Id;
            var affectedFiles = new Dictionary<string, SlnFile>
            {
                [Path.Combine(project1SrcDir, "Class1.cs")] = new SlnFile(FileType.Code, project1Id),
                [Path.Combine(project1SrcDir, "Class2.cs")] = new SlnFile(FileType.Code, project1Id)
            };

            // Act
            var cacheResult = cache.GetProjectsToRebuild(affectedFiles);

            // Get live dependency graph result
            var dependencyGraph = solution.GetProjectDependencyGraph();
            var affectedProjectId = project1Id;
            var dependentProjectIds = dependencyGraph.GetProjectsThatTransitivelyDependOnThisProject(affectedProjectId);
            var liveResult = new Dictionary<string, HashSet<string>>();
            var affectedProjectPath = solution.GetProject(affectedProjectId).FilePath;
            var dependentPaths = dependentProjectIds
                .Select(id => solution.GetProject(id).FilePath)
                .Where(path => path != null)
                .ToHashSet();
            dependentPaths.Add(affectedProjectPath!);
            liveResult[affectedProjectPath!] = dependentPaths;

            // Assert
            Assert.Equal(liveResult.Count, cacheResult.Count);
            Assert.Equal(liveResult.Keys, cacheResult.Keys);
            
            foreach (var key in liveResult.Keys)
            {
                Assert.Equal(liveResult[key].OrderBy(x => x), cacheResult[key].OrderBy(x => x));
            }

            // Verify the specific dependency chain
            var project1Dependents = cacheResult[project1Path];
            Assert.Contains(project1Path, project1Dependents);
            Assert.Contains(project2Path, project1Dependents);
            Assert.Contains(project3Path, project1Dependents);
        }
    }
} 
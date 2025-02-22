// -----------------------------------------------------------------------
// <copyright file="DependencyCacheTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Incrementalist.Caching;
using Incrementalist.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Caching
{
    public sealed class DependencyCacheTests : IDisposable
    {
        private readonly DisposableRepository _repository;
        private readonly ITestOutputHelper _output;
        
        public DependencyCacheTests(ITestOutputHelper output)
        {
            _repository = new DisposableRepository();
            _output = output;
        }
        
        public void Dispose()
        {
            _repository.Dispose();
        }
        
        [Fact]
        public async Task SaveAndLoad_PreservesAllData()
        {
            // Arrange
            var projects = new Dictionary<string, ProjectNode>
            {
                ["src/Project1/Project1.csproj"] = new(
                    "src/Project1/Project1.csproj",
                    ImmutableList<string>.Empty),
                    
                ["src/Project2/Project2.csproj"] = new(
                    "src/Project2/Project2.csproj",
                    ImmutableList.Create("src/Project1/Project1.csproj")),
                    
                ["tests/Project1.Tests/Project1.Tests.csproj"] = new(
                    "tests/Project1.Tests/Project1.Tests.csproj",
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
    }
} 
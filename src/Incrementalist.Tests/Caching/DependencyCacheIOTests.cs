#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Incrementalist.Caching;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Caching
{
    public sealed class DependencyCacheIOTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly DisposableRepository _repository;

        public DependencyCacheIOTests(ITestOutputHelper output)
        {
            _output = output;
            _repository = new DisposableRepository();
        }

        public void Dispose()
        {
            _repository.Dispose();
        }
        
        private AbsolutePath GetCachePath(string fileName)
        {
            return new AbsolutePath(Path.Combine(_repository.BasePath.Path, fileName));
        }

        [Fact]
        public void GetCachePath_ReturnsExpectedPath()
        {
            // Arrange
            var solutionDir = new AbsolutePath(Path.Combine(_repository.BasePath.Path, "MySolution"));

            // Act
            var cachePath = DependencyCacheIO.GetCachePath(solutionDir);

            // Assert
            var expectedPath =
                new AbsolutePath(Path.Combine(solutionDir.Path, ".incrementalist", "incrementalist.graphcache.json"));
            Assert.Equal(expectedPath,
                cachePath);
        }

        [Fact]
        public async Task LoadAsync_WithNonExistentFile_ReturnsNull()
        {
            // Arrange
            var cachePath = GetCachePath("nonexistent.json");

            // Act
            var result = await DependencyCacheIO.LoadAsync(cachePath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SaveAndLoadAsync_WithValidCache_PreservesData()
        {
            // Arrange
            var cachePath = GetCachePath("cache.json");
            var solutionId = SolutionId.CreateNewId();
            var projectId = ProjectId.CreateNewId();
            var cache = new DependencyCache(
                Version: IncrementalistFileConstants.CurrentVersion,
                SolutionPath: new RelativePath("test.sln"),
                Checksum: "test-checksum",
                Projects: ImmutableDictionary<ProjectId, ProjectNode>.Empty
                    .Add(projectId, new ProjectNode(
                        projectId,
                        new RelativePath("test.csproj"),
                        ImmutableList<ProjectId>.Empty)));

            // Act
            await DependencyCacheIO.SaveAsync(cachePath, cache);
            var loaded = await DependencyCacheIO.LoadAsync(cachePath);

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(cache.Version, loaded.Version);
            Assert.Equal(cache.SolutionPath, loaded.SolutionPath);
            Assert.Equal(cache.Checksum, loaded.Checksum);
            Assert.Single(loaded.Projects);
            Assert.True(loaded.Projects.ContainsKey(projectId));
        }

        [Fact]
        public async Task SaveAsync_WithNullCache_ThrowsArgumentNullException()
        {
            // Arrange
            var cachePath = GetCachePath("cache.json");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                DependencyCacheIO.SaveAsync(cachePath, null!));
        }

        [Fact]
        public async Task SaveAsync_WithNullPath_ThrowsArgumentNullException()
        {
            // Arrange
            var cache = new DependencyCache(
                Version: IncrementalistFileConstants.CurrentVersion,
                SolutionPath: new RelativePath("test.sln"),
                Checksum: "test-checksum",
                Projects: ImmutableDictionary<ProjectId, ProjectNode>.Empty);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                DependencyCacheIO.SaveAsync(null!, cache));
        }

        [Fact]
        public async Task SaveAsync_CreatesDirectoryIfNotExists()
        {
            // Arrange
            var dir = Path.Combine(_repository.BasePath.Path, "subdir");
            var cachePath = new AbsolutePath(Path.Combine(dir, "cache.json"));
            var cache = new DependencyCache(
                Version: IncrementalistFileConstants.CurrentVersion,
                SolutionPath: new RelativePath("test.sln"),
                Checksum: "test-checksum",
                Projects: ImmutableDictionary<ProjectId, ProjectNode>.Empty);

            // Act
            await DependencyCacheIO.SaveAsync(cachePath, cache);

            // Assert
            Assert.True(Directory.Exists(dir));
            Assert.True(File.Exists(cachePath.Path));
        }
    }
} 
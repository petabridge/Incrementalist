#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Incrementalist.Caching;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Caching
{
    [Collection(MSBuildCollectionFixture.Name)]
    public class DependencyCacheValidationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly DisposableRepository _repository;
        private readonly MSBuildWorkspace _workspace;

        public DependencyCacheValidationTests(ITestOutputHelper output, MSBuildFixture fixture)
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
        public async Task IsCacheValidAsync_WithNullCache_ReturnsFalse()
        {
            // Arrange
            var solution = CreateEmptySolution();
            var logger = new TestOutputLogger(_output);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(null, _repository.BasePath, solution, logger);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task IsCacheValidAsync_WithVersionMismatch_ReturnsFalse()
        {
            // Arrange
            var solution = CreateEmptySolution();
            var logger = new TestOutputLogger(_output);
            var cache = new DependencyCache(
                Version: "0.9",
                SolutionPath: solution.FilePath!,
                Checksum: "dummy-checksum",
                Projects: ImmutableDictionary<ProjectId, ProjectNode>.Empty);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(cache, _repository.BasePath, solution, logger);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task IsCacheValidAsync_WithSolutionPathMismatch_ReturnsFalse()
        {
            // Arrange
            var solution = CreateEmptySolution();
            var logger = new TestOutputLogger(_output);
            var cache = new DependencyCache(
                Version: IncrementalistFileConstants.CurrentVersion,
                SolutionPath: "different/path/solution.sln",
                Checksum: "dummy-checksum",
                Projects: ImmutableDictionary<ProjectId, ProjectNode>.Empty);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(cache, _repository.BasePath, solution, logger);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task IsCacheValidAsync_WithChecksumMismatch_ReturnsFalse()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            // Create project directories and files
            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            await File.WriteAllTextAsync(solutionPath, ProjectSampleGenerator.CreateSolutionFile("test", ["Project1", "Project2"
            ]));
            await File.WriteAllTextAsync(project1Path, ProjectSampleGenerator.CreateProjectFile("Project1"));
            await File.WriteAllTextAsync(project2Path, ProjectSampleGenerator.CreateProjectFile("Project2", ["Project1"
            ]));

            var solution = await _workspace.OpenSolutionAsync(solutionPath);
            var logger = new TestOutputLogger(_output);

            // Create cache with different checksum
            var cache = new DependencyCache(
                Version: IncrementalistFileConstants.CurrentVersion,
                SolutionPath: solution.FilePath!,
                Checksum: "different-checksum",
                Projects: ImmutableDictionary<ProjectId, ProjectNode>.Empty);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(cache, _repository.BasePath, solution, logger);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task IsCacheValidAsync_WithValidCache_ReturnsTrue()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            // Create project directories and files
            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            await File.WriteAllTextAsync(solutionPath, ProjectSampleGenerator.CreateSolutionFile("test", ["Project1", "Project2"
            ]));
            await File.WriteAllTextAsync(project1Path, ProjectSampleGenerator.CreateProjectFile("Project1"));
            await File.WriteAllTextAsync(project2Path, ProjectSampleGenerator.CreateProjectFile("Project2", ["Project1"
            ]));

            var solution = await _workspace.OpenSolutionAsync(solutionPath);
            var logger = new TestOutputLogger(_output);

            // Create valid cache
            var cache = await DependencyCacheHelper.CreateFromSolutionAsync(_repository.BasePath, solution);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(cache, _repository.BasePath, solution, logger);

            // Assert
            Assert.True(isValid);
        }

        private Solution CreateEmptySolution()
        {
            var workspace = new AdhocWorkspace();
            var solutionInfo = SolutionInfo.Create(
                SolutionId.CreateNewId(),
                VersionStamp.Create(),
                Path.Combine(_repository.BasePath, "test.sln"));
            return workspace.AddSolution(solutionInfo);
        }
    }
} 
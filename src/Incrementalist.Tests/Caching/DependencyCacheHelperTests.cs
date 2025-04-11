#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Incrementalist.Caching;
using Incrementalist.ProjectSystem.Cmds;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Caching
{
    [Collection(MSBuildCollectionFixture.Name)]
    public sealed class DependencyCacheHelperTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly DisposableRepository _repository;
        private readonly MSBuildWorkspace _workspace;
        private readonly TestOutputLogger _logger;

        public DependencyCacheHelperTests(ITestOutputHelper output, MSBuildFixture fixture)
        {
            _output = output;
            _repository = new DisposableRepository();
            _workspace = fixture.Workspace;
            _logger = new TestOutputLogger(output);
        }

        public void Dispose()
        {
            _repository.Dispose();
        }

        [Fact]
        public async Task IsCacheValidAsync_WithNullCache_ReturnsFalse()
        {
            // Arrange
            var solution = CreateEmptySolution();
            var solutionDetails = await LoadSolutionCmd.GetSolutionDetailsAsync(solution.FilePath);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(null, _repository.BasePath, solutionDetails, _logger);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task IsCacheValidAsync_WithVersionMismatch_ReturnsFalse()
        {
            // Arrange
            var solution = CreateEmptySolution();
            var solutionDetails = await LoadSolutionCmd.GetSolutionDetailsAsync(solution.FilePath);
            var cache = new DependencyCache(
                Version: "0.9",
                SolutionPath: solution.FilePath!,
                Checksum: "test-checksum",
                Projects: ImmutableDictionary<Guid, ProjectNode>.Empty);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(cache, _repository.BasePath, solutionDetails, _logger);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task IsCacheValidAsync_WithSolutionPathMismatch_ReturnsFalse()
        {
            // Arrange
            var solution = CreateEmptySolution();
            var solutionDetails = await LoadSolutionCmd.GetSolutionDetailsAsync(solution.FilePath);
            var cache = new DependencyCache(
                Version: IncrementalistFileConstants.CurrentVersion,
                SolutionPath: "different/path/solution.sln",
                Checksum: "test-checksum",
                Projects: ImmutableDictionary<Guid, ProjectNode>.Empty);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(cache, _repository.BasePath, solutionDetails, _logger);

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

            await File.WriteAllTextAsync(solutionPath, ProjectSampleGenerator.CreateSolutionFile("test", ["Project1", "Project2"]));
            await File.WriteAllTextAsync(project1Path, ProjectSampleGenerator.CreateProjectFile("Project1"));
            await File.WriteAllTextAsync(project2Path, ProjectSampleGenerator.CreateProjectFile("Project2", ["Project1"]));
            
            var solutionDetails = await LoadSolutionCmd.GetSolutionDetailsAsync(solutionPath);
            var cache = new DependencyCache(
                Version: IncrementalistFileConstants.CurrentVersion,
                SolutionPath: solutionDetails.SolutionFilePath,
                Checksum: "different-checksum",
                Projects: ImmutableDictionary<Guid, ProjectNode>.Empty);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(cache, _repository.BasePath, solutionDetails, _logger);

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

            await File.WriteAllTextAsync(solutionPath, ProjectSampleGenerator.CreateSolutionFile("test", ["Project1", "Project2"]));
            await File.WriteAllTextAsync(project1Path, ProjectSampleGenerator.CreateProjectFile("Project1"));
            await File.WriteAllTextAsync(project2Path, ProjectSampleGenerator.CreateProjectFile("Project2", ["Project1"]));

            var solution = await LoadSolutionCmd.GetSolutionDetailsAsync(solutionPath);

            // Create valid cache
            var cache = await DependencyCacheHelper.CreateFromSolutionAsync(_repository.BasePath, solution);

            // Act
            var isValid = await DependencyCacheHelper.IsCacheValidAsync(cache, _repository.BasePath, solution, _logger);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task CreateFromSolutionAsync_WithValidSolution_CreatesValidCache()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            // Create project directories and files
            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            await File.WriteAllTextAsync(solutionPath, ProjectSampleGenerator.CreateSolutionFile("test", ["Project1", "Project2"]));
            await File.WriteAllTextAsync(project1Path, ProjectSampleGenerator.CreateProjectFile("Project1"));
            await File.WriteAllTextAsync(project2Path, ProjectSampleGenerator.CreateProjectFile("Project2", ["Project1"]));

            var solution = await LoadSolutionCmd.GetSolutionDetailsAsync(solutionPath);

            // Act
            var cache = await DependencyCacheHelper.CreateFromSolutionAsync(_repository.BasePath, solution);

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(IncrementalistFileConstants.CurrentVersion, cache.Version);
            Assert.Equal(Path.GetRelativePath(_repository.BasePath, solution.SolutionFilePath!), cache.SolutionPath);
            Assert.NotNull(cache.Checksum);
            Assert.Equal(2, cache.Projects.Count);

            // Verify Project2 depends on Project1
            var project2 = solution.SolutionModel.SolutionProjects.First(p => p.DisplayName == "Project2");
            var project1Id = solution.SolutionModel.SolutionProjects.First(p => p.DisplayName == "Project1").Id;
            Assert.Contains(project1Id, cache.Projects[project2.Id].Dependencies);
        }

        [Fact]
        public async Task CreateFromSolutionAsync_WithNullSolution_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                DependencyCacheHelper.CreateFromSolutionAsync(_repository.BasePath, null!));
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
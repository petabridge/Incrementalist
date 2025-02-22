#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Incrementalist.Caching;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Caching
{
    [Collection(MSBuildCollectionFixture.Name)]
    public class DependencyCacheHelperTests : IDisposable
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DisposableRepository _repository;
        private readonly MSBuildWorkspace _workspace;

        public DependencyCacheHelperTests(ITestOutputHelper outputHelper, MSBuildFixture fixture)
        {
            _outputHelper = outputHelper;
            _repository = new DisposableRepository();
            _workspace = fixture.Workspace;
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }

        /// <summary>
        /// Verifies that a cache can be serialized to disk and deserialized back with all data preserved exactly
        /// </summary>
        private async Task VerifyCacheSerializationPreservesDataAsync(DependencyCache cache, string repositoryRootPath)
        {
            // Save cache to disk
            var cachePath = DependencyCacheIO.GetCachePath(repositoryRootPath);
            await DependencyCacheIO.SaveAsync(cachePath, cache);

            // Load cache back from disk
            var loadedCache = await DependencyCacheIO.LoadAsync(cachePath);

            // Verify all properties match
            Assert.NotNull(loadedCache);
            Assert.Equal(cache.Projects.Count, loadedCache.Projects.Count);
            foreach (var (projectPath, projectInfo) in cache.Projects)
            {
                Assert.True(loadedCache.Projects.ContainsKey(projectPath));
                Assert.Equal(projectInfo.Dependencies, loadedCache.Projects[projectPath].Dependencies);
            }
        }

        [Fact]
        public async Task CreateFromSolutionAsync_WithNullSolution_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => DependencyCacheHelper.CreateFromSolutionAsync(null!, "dummy"));
        }

        [Fact]
        public async Task CreateFromSolutionAsync_WithNullRepositoryRootPath_ThrowsArgumentNullException()
        {
            var solution = await _workspace.OpenSolutionAsync("dummy.sln");
            await Assert.ThrowsAsync<ArgumentNullException>(() => DependencyCacheHelper.CreateFromSolutionAsync(solution, null!));
        }

        [Fact]
        public async Task CreateFromSolutionAsync_WithSimpleSolution_CreatesValidCache()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            // Create project directories
            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            // Create initial solution and project files
            await File.WriteAllTextAsync(solutionPath, @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project1"", ""src\Project1\Project1.csproj"", ""{72bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project2"", ""src\Project2\Project2.csproj"", ""{49bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
");

            await File.WriteAllTextAsync(project1Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>
</Project>");

            await File.WriteAllTextAsync(project2Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Project1\Project1.csproj"" />
  </ItemGroup>
</Project>");

            // Load solution
            var solution = await _workspace.OpenSolutionAsync(solutionPath);

            // Act
            var cache = await DependencyCacheHelper.CreateFromSolutionAsync(solution, _repository.BasePath);

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(2, cache.Projects.Count);

            var relativeProject1Path = "src/Project1/Project1.csproj";
            var relativeProject2Path = "src/Project2/Project2.csproj";

            // Project1 has no dependencies
            Assert.True(cache.Projects.ContainsKey(relativeProject1Path));
            Assert.Empty(cache.Projects[relativeProject1Path].Dependencies);

            // Project2 depends on Project1
            Assert.True(cache.Projects.ContainsKey(relativeProject2Path));
            Assert.Single(cache.Projects[relativeProject2Path].Dependencies);
            Assert.Equal(relativeProject1Path, cache.Projects[relativeProject2Path].Dependencies[0]);

            // Verify cache can be saved and loaded correctly
            await VerifyCacheSerializationPreservesDataAsync(cache, _repository.BasePath);
        }

        [Fact]
        public async Task CreateFromSolutionAsync_WithTransitiveDependencies_CreatesValidCache()
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

            // Create initial solution and project files
            await File.WriteAllTextAsync(solutionPath, @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project1"", ""src\Project1\Project1.csproj"", ""{72bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project2"", ""src\Project2\Project2.csproj"", ""{49bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project3"", ""src\Project3\Project3.csproj"", ""{39bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
");

            await File.WriteAllTextAsync(project1Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>
</Project>");

            await File.WriteAllTextAsync(project2Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Project1\Project1.csproj"" />
  </ItemGroup>
</Project>");

            await File.WriteAllTextAsync(project3Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Project2\Project2.csproj"" />
  </ItemGroup>
</Project>");

            // Load solution
            var solution = await _workspace.OpenSolutionAsync(solutionPath);

            // Act
            var cache = await DependencyCacheHelper.CreateFromSolutionAsync(solution, _repository.BasePath);

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(3, cache.Projects.Count);

            var relativeProject1Path = "src/Project1/Project1.csproj";
            var relativeProject2Path = "src/Project2/Project2.csproj";
            var relativeProject3Path = "src/Project3/Project3.csproj";

            // Project1 has no dependencies
            Assert.True(cache.Projects.ContainsKey(relativeProject1Path));
            Assert.Empty(cache.Projects[relativeProject1Path].Dependencies);

            // Project2 depends on Project1
            Assert.True(cache.Projects.ContainsKey(relativeProject2Path));
            Assert.Single(cache.Projects[relativeProject2Path].Dependencies);
            Assert.Equal(relativeProject1Path, cache.Projects[relativeProject2Path].Dependencies[0]);

            // Project3 depends on Project2 (but not directly on Project1)
            Assert.True(cache.Projects.ContainsKey(relativeProject3Path));
            Assert.Single(cache.Projects[relativeProject3Path].Dependencies);
            Assert.Equal(relativeProject2Path, cache.Projects[relativeProject3Path].Dependencies[0]);

            // Verify cache can be saved and loaded correctly
            await VerifyCacheSerializationPreservesDataAsync(cache, _repository.BasePath);
        }

        [Fact]
        public async Task CreateFromSolutionAsync_WithModifiedProject_GeneratesDifferentChecksum()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            // Create project directories
            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            // Create initial solution and project files
            await File.WriteAllTextAsync(solutionPath, @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project1"", ""src\Project1\Project1.csproj"", ""{72bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project2"", ""src\Project2\Project2.csproj"", ""{49bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
");

            await File.WriteAllTextAsync(project1Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>
</Project>");

            await File.WriteAllTextAsync(project2Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Project1\Project1.csproj"" />
  </ItemGroup>
</Project>");

            // Load solution
            var solution = await _workspace.OpenSolutionAsync(solutionPath);

            // Create initial cache
            var initialCache = await DependencyCacheHelper.CreateFromSolutionAsync(solution, _repository.BasePath);

            // Modify Project1
            await File.WriteAllTextAsync(project1Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>");

            // Reload solution and create new cache
            solution = await _workspace.OpenSolutionAsync(solutionPath);
            var modifiedCache = await DependencyCacheHelper.CreateFromSolutionAsync(solution, _repository.BasePath);

            // Assert
            Assert.NotEqual(initialCache.Checksum, modifiedCache.Checksum);

            // Verify both caches can be saved and loaded correctly
            await VerifyCacheSerializationPreservesDataAsync(initialCache, _repository.BasePath);
            await VerifyCacheSerializationPreservesDataAsync(modifiedCache, _repository.BasePath);
        }
    }
} 
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
    public sealed class DependencyCacheHelperTests : IDisposable
    {
        private readonly DisposableRepository _repository;
        private readonly ITestOutputHelper _output;
        private readonly MSBuildWorkspace _workspace;

        public DependencyCacheHelperTests(ITestOutputHelper output)
        {
            _repository = new DisposableRepository();
            _output = output;
            _workspace = MSBuildWorkspace.Create();
        }

        public void Dispose()
        {
            _workspace.Dispose();
            _repository.Dispose();
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

            // Create solution and project files
            await File.WriteAllTextAsync(solutionPath, @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project1"", ""src\Project1\Project1.csproj"", ""{72bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project2"", ""src\Project2\Project2.csproj"", ""{49bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
EndGlobal");

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

            var solution = await _workspace.OpenSolutionAsync(solutionPath);

            // Act
            var cache = await DependencyCacheHelper.CreateFromSolutionAsync(solution);

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(DependencyCacheIO.CurrentVersion, cache.Version);
            Assert.Equal(solutionPath, cache.SolutionPath);
            Assert.NotNull(cache.Checksum);
            Assert.Equal(2, cache.Projects.Count);

            // Project1 has no dependencies
            Assert.True(cache.Projects.ContainsKey(project1Path));
            Assert.Empty(cache.Projects[project1Path].Dependencies);

            // Project2 depends on Project1
            Assert.True(cache.Projects.ContainsKey(project2Path));
            Assert.Single(cache.Projects[project2Path].Dependencies);
            Assert.Equal(project1Path, cache.Projects[project2Path].Dependencies[0]);
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

            // Create solution and project files
            await File.WriteAllTextAsync(solutionPath, @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project1"", ""src\Project1\Project1.csproj"", ""{72bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project2"", ""src\Project2\Project2.csproj"", ""{49bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project3"", ""src\Project3\Project3.csproj"", ""{39bdc44f-c588-44f3-b6df-9aace7daafdd}""
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
EndGlobal");

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

            var solution = await _workspace.OpenSolutionAsync(solutionPath);

            // Act
            var cache = await DependencyCacheHelper.CreateFromSolutionAsync(solution);

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(3, cache.Projects.Count);

            // Project1 has no dependencies
            Assert.True(cache.Projects.ContainsKey(project1Path));
            Assert.Empty(cache.Projects[project1Path].Dependencies);

            // Project2 depends on Project1
            Assert.True(cache.Projects.ContainsKey(project2Path));
            Assert.Single(cache.Projects[project2Path].Dependencies);
            Assert.Equal(project1Path, cache.Projects[project2Path].Dependencies[0]);

            // Project3 depends on Project2 (but not directly on Project1)
            Assert.True(cache.Projects.ContainsKey(project3Path));
            Assert.Single(cache.Projects[project3Path].Dependencies);
            Assert.Equal(project2Path, cache.Projects[project3Path].Dependencies[0]);
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
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
EndGlobal");

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

            var solution = await _workspace.OpenSolutionAsync(solutionPath);
            var initialCache = await DependencyCacheHelper.CreateFromSolutionAsync(solution);

            // Modify Project1
            await File.WriteAllTextAsync(project1Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");
            
            // Reload solution
            _workspace.CloseSolution();
            solution = await _workspace.OpenSolutionAsync(solutionPath);
            
            // Act
            var modifiedCache = await DependencyCacheHelper.CreateFromSolutionAsync(solution);

            // Assert
            Assert.NotEqual(initialCache.Checksum, modifiedCache.Checksum);
        }

        [Fact]
        public async Task CreateFromSolutionAsync_WithNullSolution_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                DependencyCacheHelper.CreateFromSolutionAsync(null!));
        }
    }
} 
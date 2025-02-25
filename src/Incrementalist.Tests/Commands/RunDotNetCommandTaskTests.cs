// -----------------------------------------------------------------------
// <copyright file="RunDotNetCommandTaskTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2023 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Incrementalist.Cmd.Commands;
using Incrementalist.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Commands
{
    public class RunDotNetCommandTaskTests : IDisposable
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly TestOutputLogger _logger;
        private readonly DisposableRepository _repository;

        public RunDotNetCommandTaskTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _logger = new TestOutputLogger(outputHelper);
            _repository = new DisposableRepository();
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }

        [Fact]
        public async Task Should_Execute_Command_Successfully()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var projectPath = Path.Combine(_repository.BasePath, "test.csproj");
            await File.WriteAllTextAsync(projectPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>
</Project>");
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "build", "-c", "Release", "--nologo" }, true, false);

            // Act
            var result = await task.Run(new IncrementalBuildResult(new[] { projectPath }));

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_Handle_Failed_Command()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "build", "--invalid-option" }, true, false);

            // Act
            var result = await task.Run(new IncrementalBuildResult(new[] { "dummy.csproj" }));

            // Assert
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task Should_Run_Commands_In_Parallel()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var projects = new List<string>();
            for (int i = 1; i <= 3; i++)
            {
                var projectPath = Path.Combine(_repository.BasePath, $"test{i}.csproj");
                await File.WriteAllTextAsync(projectPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>
</Project>");
                projects.Add(projectPath);
            }
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "build", "-c", "Release", "--nologo" }, true, true);

            // Act
            var result = await task.Run(new IncrementalBuildResult(projects));

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_Stop_On_First_Failure_When_ContinueOnError_False()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "invalid-command" }, false, false);
            var projects = new[] { "project1.csproj", "project2.csproj" };

            // Act
            var result = await task.Run(new IncrementalBuildResult(projects));

            // Assert
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task Should_Execute_Full_Solution_Build()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            
            // Create a minimal valid solution file
            var solutionContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
EndGlobal";
            File.WriteAllText(solutionPath, solutionContent);
            
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "build", "-c", "Release", "--nologo" }, true, false);

            // Act
            var result = await task.Run(new FullSolutionBuildResult(solutionPath));

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_Handle_Arguments_With_Spaces()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var args = new[]
            {
                "test",
                "--logger:trx",
                "--collect:\"XPlat Code Coverage\"",
                "--results-directory:\"Test Results\"",
            };
            var task = new RunDotNetCommandTask(settings, _logger, args, true, false);
            var projectPath = "MyProject.csproj";

            // Act
            var result = new IncrementalBuildResult(new[] { projectPath });
            var exitCode = await task.Run(result);

            // Assert
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task Should_Handle_Arguments_With_Embedded_Quotes()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var args = new[]
            {
                "test",
                "--logger:\"console;verbosity=detailed\"",
                "--collect:\"XPlat Code Coverage;Format=cobertura\"",
            };
            var task = new RunDotNetCommandTask(settings, _logger, args, true, false);
            var projectPath = "MyProject.csproj";

            // Act
            var result = new IncrementalBuildResult(new[] { projectPath });
            var exitCode = await task.Run(result);

            // Assert
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task Should_Handle_Windows_Paths()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var args = new[]
            {
                "test",
                "--results-directory:\"C:\\Test Results\"",
                "--logger:\"trx;LogFileName=C:\\Test Results\\test.trx\"",
            };
            var task = new RunDotNetCommandTask(settings, _logger, args, true, false);
            var projectPath = "MyProject.csproj";

            // Act
            var result = new IncrementalBuildResult(new[] { projectPath });
            var exitCode = await task.Run(result);

            // Assert
            Assert.Equal(0, exitCode);
        }
    }
} 
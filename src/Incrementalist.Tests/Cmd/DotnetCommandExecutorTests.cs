using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Incrementalist.Cmd;
using Incrementalist.Cmd.Commands;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Cmd
{
    public class DotnetCommandExecutorTests
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        private readonly string _testProjectPath;

        public DotnetCommandExecutorTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = XUnitLogger.CreateLogger(_output);
            _testProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Incrementalist.Tests.csproj"));
        }

        [Fact]
        public async Task ShouldSkipExecutionWhenNoCommandsSpecified()
        {
            // Arrange
            var options = new SlnOptions();
            var executor = new DotnetCommandExecutor(options, _logger);

            // Act
            var result = await executor.ExecuteCommandsOnProjects(new[] { "dummy.csproj" });

            // Assert
            Assert.True(result, "Should return true when no commands are specified");
        }

        [Fact]
        public async Task ShouldHandleEmptyProjectList()
        {
            // Arrange
            var options = new SlnOptions { DotnetCommands = new[] { "build" } };
            var executor = new DotnetCommandExecutor(options, _logger);

            // Act
            var result = await executor.ExecuteCommandsOnProjects(Array.Empty<string>());

            // Assert
            Assert.True(result, "Should return true for empty project list");
        }

        [Fact]
        public async Task ShouldBuildValidProject()
        {
            // Arrange
            var options = new SlnOptions 
            { 
                DotnetCommands = new[] { "build" },
                Configuration = "Debug",
                NoRestore = true // Skip restore to speed up the test
            };
            var executor = new DotnetCommandExecutor(options, _logger);

            // Act
            var result = await executor.ExecuteCommandsOnProjects(new[] { _testProjectPath });

            // Assert
            Assert.True(result, "Should successfully build a valid project");
        }

        [Fact]
        public async Task ShouldFailOnInvalidProject()
        {
            // Arrange
            var options = new SlnOptions 
            { 
                DotnetCommands = new[] { "build" },
                Configuration = "Debug"
            };
            var executor = new DotnetCommandExecutor(options, _logger);

            // Act
            var result = await executor.ExecuteCommandsOnProjects(new[] { "nonexistent.csproj" });

            // Assert
            Assert.False(result, "Should fail when project doesn't exist");
        }

        [Fact]
        public async Task ShouldApplyAllCommandLineOptions()
        {
            // Arrange
            var options = new SlnOptions 
            { 
                DotnetCommands = new[] { "build" },
                Configuration = "Debug",
                Framework = "net8.0", // Use .NET 8.0 since it's installed
                NoRestore = true
            };
            var executor = new DotnetCommandExecutor(options, _logger);

            // Act
            var result = await executor.ExecuteCommandsOnProjects(new[] { _testProjectPath });

            // Assert
            Assert.True(result, "Should successfully build with all options specified");
        }

        [Fact]
        public async Task ShouldExecuteMultipleCommandsSequentially()
        {
            // Arrange
            var options = new SlnOptions 
            { 
                DotnetCommands = new[] { "clean", "build" },
                Configuration = "Debug",
                NoRestore = true
            };
            var executor = new DotnetCommandExecutor(options, _logger);

            // Act
            var result = await executor.ExecuteCommandsOnProjects(new[] { _testProjectPath });

            // Assert
            Assert.True(result, "Should successfully execute multiple commands");
        }

        [Fact]
        public async Task ShouldStopOnFirstFailure()
        {
            // Arrange
            var options = new SlnOptions 
            { 
                DotnetCommands = new[] { "build", "test" },
                Configuration = "Debug"
            };
            var executor = new DotnetCommandExecutor(options, _logger);

            // Act
            var result = await executor.ExecuteCommandsOnProjects(new[] { "nonexistent.csproj" });

            // Assert
            Assert.False(result, "Should fail on first command and not execute subsequent commands");
        }
    }
}
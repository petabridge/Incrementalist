using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Incrementalist.ProjectSystem;
using Xunit;

namespace Incrementalist.Tests.ProjectSystem
{
    public class DotNetCommandOptimizerTests
    {
        private readonly DotNetCommandOptimizer _optimizer;
        private readonly string _solutionFile = "test.sln";

        public DotNetCommandOptimizerTests()
        {
            _optimizer = new DotNetCommandOptimizer();
        }

        [Theory]
        [InlineData("Directory.Build.props")]
        [InlineData("Directory.Packages.props")]
        [InlineData("global.json")]
        [InlineData("NuGet.config")]
        [InlineData(".editorconfig")]
        [InlineData("test.sln")]
        public void Should_Detect_Solution_Level_Files(string fileName)
        {
            // Arrange
            var files = new[] { fileName };

            // Act
            var shouldRunOnSolution = _optimizer.ShouldRunOnFullSolution(files);

            // Assert
            shouldRunOnSolution.Should().BeTrue();
        }

        [Fact]
        public void Should_Not_Detect_Regular_Project_Files_As_Solution_Level()
        {
            // Arrange
            var files = new[] { "src/Project1/Project1.csproj", "src/Project2/Project2.fsproj" };

            // Act
            var shouldRunOnSolution = _optimizer.ShouldRunOnFullSolution(files);

            // Assert
            shouldRunOnSolution.Should().BeFalse();
        }

        [Theory]
        [InlineData("test")]
        [InlineData("pack")]
        [InlineData("publish")]
        public void Should_Return_Individual_Commands_For_Non_Build_Commands(string command)
        {
            // Arrange
            var projects = new[] { "Project1.csproj", "Project2.csproj" };

            // Act
            var commands = _optimizer.OptimizeCommand(command, projects, _solutionFile).ToList();

            // Assert
            commands.Should().HaveCount(2);
            commands.Should().Contain($"{command} Project1.csproj");
            commands.Should().Contain($"{command} Project2.csproj");
        }

        [Fact]
        public void Should_Return_Single_Command_For_Build()
        {
            // Arrange
            var projects = new[] { "Project1.csproj", "Project2.csproj" };

            // Act
            var commands = _optimizer.OptimizeCommand("build", projects, _solutionFile).ToList();

            // Assert
            commands.Should().HaveCount(1);
            commands[0].Should().Be("build Project1.csproj Project2.csproj");
        }

        [Fact]
        public void Should_Preserve_Build_Arguments()
        {
            // Arrange
            var projects = new[] { "Project1.csproj", "Project2.csproj" };
            var buildCommand = "build -c Release --no-restore";

            // Act
            var commands = _optimizer.OptimizeCommand(buildCommand, projects, _solutionFile).ToList();

            // Assert
            commands.Should().HaveCount(1);
            commands[0].Should().Be("build -c Release --no-restore Project1.csproj Project2.csproj");
        }

        [Theory]
        [InlineData("test -c Release")]
        [InlineData("pack -c Debug --no-restore")]
        [InlineData("publish -c Release --no-build")]
        public void Should_Preserve_Command_Arguments_For_Individual_Commands(string command)
        {
            // Arrange
            var projects = new[] { "Project1.csproj", "Project2.csproj" };

            // Act
            var commands = _optimizer.OptimizeCommand(command, projects, _solutionFile).ToList();

            // Assert
            commands.Should().HaveCount(2);
            commands.Should().Contain($"{command} Project1.csproj");
            commands.Should().Contain($"{command} Project2.csproj");
        }

        [Fact]
        public void Should_Handle_Empty_Project_List()
        {
            // Arrange
            var projects = new string[0];

            // Act
            var commands = _optimizer.OptimizeCommand("build", projects, _solutionFile).ToList();

            // Assert
            commands.Should().HaveCount(1);
            commands[0].Should().Be("build ");
        }

        [Fact]
        public void Should_Handle_Single_Project()
        {
            // Arrange
            var projects = new[] { "Project1.csproj" };

            // Act
            var buildCommands = _optimizer.OptimizeCommand("build", projects, _solutionFile).ToList();
            var testCommands = _optimizer.OptimizeCommand("test", projects, _solutionFile).ToList();

            // Assert
            buildCommands.Should().HaveCount(1);
            buildCommands[0].Should().Be("build Project1.csproj");

            testCommands.Should().HaveCount(1);
            testCommands[0].Should().Be("test Project1.csproj");
        }
    }
} 
// -----------------------------------------------------------------------
// <copyright file="RunDotNetCommandTaskTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2023 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
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
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "--version" }, true, false);

            // Act
            var result = await task.Run(new[] { "dummy.csproj" });

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public async Task Should_Handle_Failed_Command()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "invalid-command" }, true, false);

            // Act
            var result = await task.Run(new[] { "dummy.csproj" });

            // Assert
            result.Should().Be(1);
        }

        [Fact]
        public async Task Should_Run_Commands_In_Parallel()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "--version" }, true, true);
            var projects = new[] { "project1.csproj", "project2.csproj", "project3.csproj" };

            // Act
            var result = await task.Run(projects);

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public async Task Should_Stop_On_First_Failure_When_ContinueOnError_False()
        {
            // Arrange
            var settings = new BuildSettings("master", "test.sln", _repository.BasePath);
            var task = new RunDotNetCommandTask(settings, _logger, new[] { "invalid-command" }, false, false);
            var projects = new[] { "project1.csproj", "project2.csproj" };

            // Act
            var result = await task.Run(projects);

            // Assert
            result.Should().Be(1);
        }
    }
} 
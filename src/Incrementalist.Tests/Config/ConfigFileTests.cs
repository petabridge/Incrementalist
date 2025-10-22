// -----------------------------------------------------------------------
// <copyright file="ConfigFileTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Incrementalist.Cmd;
using Incrementalist.Cmd.Commands;
using Incrementalist.Cmd.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using RunOptions = Incrementalist.Cmd.RunOptions;

namespace Incrementalist.Tests.Config
{
    public class ConfigFileTests
    {
        [Fact]
        public void Config_File_Loading_Success()
        {
            // Arrange
            var configPath = Path.GetTempFileName();
            var config = new IncrementalistConfig
            {
                GitBranch = "master",
                SolutionFilePath = "MySolution.sln",
                TimeoutMinutes = 5,
                Verbose = true,
                RunInParallel = true,
                ParallelLimit = 5
            };

            try
            {
                // Act - write the config to a file
                File.WriteAllText(configPath, JsonSerializer.Serialize(config));

                // Assert - verify it loads correctly
                Assert.True(IncrementalistConfig.TryLoad(configPath, out var loadedConfig));
                Assert.NotNull(loadedConfig);
                Assert.Equal("master", loadedConfig.GitBranch);
                Assert.Equal("MySolution.sln", loadedConfig.SolutionFilePath);
                Assert.Equal(5, loadedConfig.TimeoutMinutes);
                Assert.True(loadedConfig.Verbose);
                Assert.True(loadedConfig.RunInParallel);
                Assert.Equal(5, loadedConfig.ParallelLimit);
                Assert.Null(loadedConfig.ListFolders);
                Assert.Null(loadedConfig.ContinueOnError);
            }
            finally
            {
                // Clean up
                File.Delete(configPath);
            }
        }

        [Fact]
        public void Config_File_Loading_With_Schema_Property()
        {
            // Arrange
            var configPath = Path.GetTempFileName();
            var configJson = @"{
                ""$schema"": ""https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json"",
                ""gitBranch"": ""master"",
                ""solutionFilePath"": ""MySolution.sln"",
                ""verbose"": true
            }";

            try
            {
                // Act - write the config with schema to a file
                File.WriteAllText(configPath, configJson);

                // Assert - verify it loads correctly and ignores the $schema property
                Assert.True(IncrementalistConfig.TryLoad(configPath, out var loadedConfig));
                Assert.NotNull(loadedConfig);
                Assert.Equal("master", loadedConfig.GitBranch);
                Assert.Equal("MySolution.sln", loadedConfig.SolutionFilePath);
                Assert.True(loadedConfig.Verbose);
            }
            finally
            {
                // Clean up
                File.Delete(configPath);
            }
        }

        [Fact]
        public void Config_File_Loading_NonExistent_File()
        {
            // Act & Assert
            Assert.False(IncrementalistConfig.TryLoad("non_existent_file.json", out var config));
            Assert.Null(config);
        }

        [Fact]
        public void Config_File_Loading_Invalid_Json()
        {
            // Arrange
            var configPath = Path.GetTempFileName();

            try
            {
                // Act - write invalid JSON
                File.WriteAllText(configPath, "{ this is not valid json }");

                // Assert
                Assert.False(IncrementalistConfig.TryLoad(configPath, out var config));
                Assert.Null(config);
            }
            finally
            {
                // Clean up
                File.Delete(configPath);
            }
        }

        [Fact]
        public void Merge_Config_With_CommandLine_Options()
        {
            // Arrange
            var config = new IncrementalistConfig
            {
                GitBranch = "master",
                SolutionFilePath = "MySolution.sln",
                TimeoutMinutes = 5,
                Verbose = true,
                RunInParallel = true,
                ParallelLimit = 5,
                OutputFile = "output.txt",
                SkipGlob = ["**/obj/**", "**/bin/**"],
                TargetGlob = ["src/**/*.csproj", "tests/**/*.csproj"]
            };

            var options = new RunOptions()
            {
                // CLI options - should override config
                GitBranch = "dev",
                TimeoutMinutes = 3,

                // Run command options that don't come from config
                DotNetArgs = ["build", "--configuration", "Release"]
            };

            // Act
            var merged = (RunOptions)ConfigMerger.Merge(options, config);

            // Assert - CLI options should override config
            Assert.Equal("dev", merged.GitBranch); // From CLI
            Assert.Equal(3, merged.TimeoutMinutes); // From CLI

            // Config values should be used when CLI doesn't specify
            Assert.Equal("MySolution.sln", merged.SolutionFilePath); // From config
            Assert.Equal("output.txt", merged.OutputFile); // From config
            Assert.True(merged.Verbose); // From config
            Assert.True(merged.RunInParallel); // From config
            Assert.Equal(5, merged.ParallelLimit); // From config
            Assert.Equivalent(config.SkipGlob, merged.SkipGlobs); // From config
            Assert.Equivalent(config.TargetGlob, merged.TargetGlobs); // From config

            // Command-specific options should be preserved
            Assert.Equal(3, merged.DotNetArgs.Length);
        }

        [Fact]
        public void Default_Values_Applied_After_Merging()
        {
            // Arrange
            var options = new ListFoldersOptions();

            // Act - Apply defaults to empty options
            var result = ConfigMerger.ApplyDefaults(options);

            // Assert - All defaults should be applied
            Assert.Same(options, result); // Should return the same instance
            Assert.Equal("dev", options.GitBranch);
            Assert.Equal(2, options.TimeoutMinutes);
            Assert.False(options.Verbose);
            Assert.True(options.ContinueOnError);
            Assert.False(options.RunInParallel);
            Assert.Equal(0, options.ParallelLimit);
            Assert.False(options.FailOnNoProjects);
        }

        [Fact]
        public async Task CreateConfigFile_WithGlobs_RoundTripSerialization()
        {
            // Arrange
            var configPath = Path.GetTempFileName();
            var skipGlobs = new[] { "**/obj/**", "**/bin/**" };
            var targetGlobs = new[] { "src/**/*.csproj", "tests/**/*.csproj" };

            var options = new CreateConfigOptions()
            {
                ConfigFile = configPath, // Specify path for CreateConfigFileTask
                SkipGlobs = skipGlobs,
                TargetGlobs = targetGlobs
            };

            var createConfigTask = new CreateConfigFileTask(options, NullLogger.Instance);

            try
            {
                // Act
                var exitCode = await createConfigTask.Run();
                Assert.Equal(0, exitCode);

                // Assert - load the config back and verify globs
                Assert.True(IncrementalistConfig.TryLoad(configPath, out var loadedConfig));
                Assert.NotNull(loadedConfig);

                Assert.Equivalent(skipGlobs, loadedConfig.SkipGlob);
                Assert.Equivalent(targetGlobs, loadedConfig.TargetGlob);
            }
            finally
            {
                // Clean up
                if (File.Exists(configPath))
                    File.Delete(configPath);
            }
        }

        [Fact]
        public async Task CreateConfigFile_Includes_JsonSchema()
        {
            // Arrange
            var configPath = Path.GetTempFileName();
            var options = new CreateConfigOptions()
            {
                ConfigFile = configPath,
                GitBranch = "master",
                Verbose = true
            };

            var createConfigTask = new CreateConfigFileTask(options, NullLogger.Instance);

            try
            {
                // Act
                var exitCode = await createConfigTask.Run();
                Assert.Equal(0, exitCode);

                // Assert - verify the generated file includes the JSON schema
                Assert.True(File.Exists(configPath));
                var configContent = await File.ReadAllTextAsync(configPath);
                
                // Check that the $schema property is included
                Assert.Contains("\"$schema\"", configContent);
                Assert.Contains("incrementalist.schema.json", configContent);
                
                // Verify the config can still be loaded normally
                Assert.True(IncrementalistConfig.TryLoad(configPath, out var loadedConfig));
                Assert.NotNull(loadedConfig);
                Assert.Equal("master", loadedConfig.GitBranch);
                Assert.True(loadedConfig.Verbose);
            }
            finally
            {
                // Clean up
                if (File.Exists(configPath))
                    File.Delete(configPath);
            }
        }
    }
}

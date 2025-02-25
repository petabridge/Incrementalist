// -----------------------------------------------------------------------
// <copyright file="ConfigFileTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2023 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Text.Json;
using Incrementalist.Cmd;
using Incrementalist.Cmd.Config;
using Xunit;

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
                RunInParallel = true
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
                OutputFile = "output.txt"
            };

            var options = new SlnOptions
            {
                // CLI options - should override config
                GitBranch = "dev",
                TimeoutMinutes = 3,
                
                // Run command options that don't come from config
                RunCommand = true,
                DotNetArgs = new[] { "build", "--configuration", "Release" }
            };

            // Act
            var merged = ConfigMerger.Merge(options, config);

            // Assert - CLI options should override config
            Assert.Equal("dev", merged.GitBranch); // From CLI
            Assert.Equal(3, merged.TimeoutMinutes); // From CLI
            
            // Config values should be used when CLI doesn't specify
            Assert.Equal("MySolution.sln", merged.SolutionFilePath); // From config
            Assert.Equal("output.txt", merged.OutputFile); // From config
            Assert.True(merged.Verbose); // From config
            Assert.True(merged.RunInParallel); // From config
            
            // Command-specific options should be preserved
            Assert.True(merged.RunCommand);
            Assert.Equal(3, merged.DotNetArgs.Length);
        }

        [Fact]
        public void Default_Values_Applied_After_Merging()
        {
            // Arrange
            var options = new SlnOptions();

            // Act - Apply defaults to empty options
            var result = ConfigMerger.ApplyDefaults(options);

            // Assert - All defaults should be applied
            Assert.Same(options, result); // Should return the same instance
            Assert.Equal("dev", options.GitBranch);
            Assert.Equal(2, options.TimeoutMinutes);
            Assert.False(options.ListFolders);
            Assert.False(options.Verbose);
            Assert.True(options.ContinueOnError);
            Assert.False(options.RunInParallel);
            Assert.False(options.FailOnNoProjects);
            Assert.False(options.NoCache);
        }
    }
} 
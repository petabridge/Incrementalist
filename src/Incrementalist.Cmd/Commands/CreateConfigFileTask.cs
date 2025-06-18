// -----------------------------------------------------------------------
// <copyright file="CreateConfigFileTask.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Incrementalist.Cmd.Config;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd.Commands
{
    /// <summary>
    /// Command to create a configuration file from the current options
    /// </summary>
    public sealed class CreateConfigFileTask
    {
        private readonly CreateConfigOptions _options;
        private readonly ILogger _logger;

        /// <summary>
        /// The JSON schema URL for Incrementalist configuration files
        /// </summary>
        private const string SchemaUrl = "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json";

        public CreateConfigFileTask(CreateConfigOptions options, ILogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Configuration object that includes the JSON schema reference
        /// </summary>
        private class ConfigWithSchema : IncrementalistConfig
        {
            [JsonPropertyName("$schema")]
            public string Schema { get; set; } = SchemaUrl;
        }

        /// <summary>
        /// Creates a configuration file from the current options
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns 0 on success, non-zero on failure.</returns>
        public async Task<int> Run()
        {
            try
            {
                // Create the config object from the current options
                var config = new ConfigWithSchema
                {
                    GitBranch = _options.GitBranch,
                    SolutionFilePath = _options.SolutionFilePath,
                    OutputFile = _options.OutputFile,
                    WorkingDirectory = _options.WorkingDirectory,
                    Verbose = _options.Verbose,
                    TimeoutMinutes = _options.TimeoutMinutes,
                    ContinueOnError = _options.ContinueOnError,
                    RunInParallel = _options.RunInParallel,
                    FailOnNoProjects = _options.FailOnNoProjects,
                    SkipGlob = _options.SkipGlobs?.ToArray(),
                    TargetGlob = _options.TargetGlobs?.ToArray(),
                    NameApplicationToStart = _options.NameApplicationToStart,
                };

                // Determine the output file path
                var configFilePath = _options.ConfigFile;
                if (string.IsNullOrEmpty(configFilePath))
                {
                    // Use default filename in the same directory as the dependency cache
                    var workingDir = _options.WorkingDirectory ?? Directory.GetCurrentDirectory();
                    var incrementalistDir =
                        Path.Combine(workingDir, IncrementalistFileConstants.IncrementalistDirectory);

                    // Create the directory if it doesn't exist
                    if (!Directory.Exists(incrementalistDir))
                    {
                        Directory.CreateDirectory(incrementalistDir);
                    }

                    configFilePath = Path.Combine(incrementalistDir, IncrementalistConfig.DefaultConfigFileName);
                }

                // Create the serialization options
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                // Serialize and write the config to file
                var json = JsonSerializer.Serialize(config, options);
                await File.WriteAllTextAsync(configFilePath, json);

                _logger.LogInformation("Configuration file created: {FilePath}", configFilePath);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating configuration file");
                return -1;
            }
        }
    }
}
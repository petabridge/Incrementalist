// -----------------------------------------------------------------------
// <copyright file="IncrementalistConfig.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2023 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Incrementalist.Cmd.Config
{
    /// <summary>
    /// Represents the configuration file for Incrementalist.
    /// Maps to the same properties as <see cref="SlnOptions"/> that make sense to persist.
    /// </summary>
    public class IncrementalistConfig
    {
        /// <summary>
        /// The default configuration file name
        /// </summary>
        public const string DefaultConfigFileName = "incrementalist.json";
        
        /// <summary>
        /// The default directory for Incrementalist files (cache, config)
        /// </summary>
        public const string IncrementalistDirectory = ".incrementalist";

        /// <summary>
        /// The name of the Solution file to be analyzed by Incrementalist.
        /// </summary>
        [JsonPropertyName("solutionFilePath")]
        public string? SolutionFilePath { get; set; }

        /// <summary>
        /// If specified, writes the output to the named file.
        /// </summary>
        [JsonPropertyName("outputFile")]
        public string? OutputFile { get; set; }

        /// <summary>
        /// List affected folders instead of .NET projects
        /// </summary>
        [JsonPropertyName("listFolders")]
        public bool? ListFolders { get; set; }

        /// <summary>
        /// The git branch to compare against. i.e. the `dev` or the `master` branch.
        /// </summary>
        [JsonPropertyName("gitBranch")]
        public string? GitBranch { get; set; }

        /// <summary>
        /// Specify the working directory explicitly. Defaults to using the current working directory.
        /// </summary>
        [JsonPropertyName("workingDirectory")]
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Prints out extensive debug logs during operation.
        /// </summary>
        [JsonPropertyName("verbose")]
        public bool? Verbose { get; set; }

        /// <summary>
        /// Specifies the load timeout for the solution in whole minutes. Defaults to 2 minutes.
        /// </summary>
        [JsonPropertyName("timeoutMinutes")]
        public int? TimeoutMinutes { get; set; }

        /// <summary>
        /// When running commands, continue executing even if some commands fail.
        /// </summary>
        [JsonPropertyName("continueOnError")]
        public bool? ContinueOnError { get; set; }

        /// <summary>
        /// When running commands, execute them in parallel.
        /// </summary>
        [JsonPropertyName("runInParallel")]
        public bool? RunInParallel { get; set; }

        /// <summary>
        /// When running commands, fail if no projects are affected.
        /// </summary>
        [JsonPropertyName("failOnNoProjects")]
        public bool? FailOnNoProjects { get; set; }

        /// <summary>
        /// Ignore any existing cache file and perform a full Roslyn analysis.
        /// </summary>
        [JsonPropertyName("noCache")]
        public bool? NoCache { get; set; }

        /// <summary>
        /// Tries to load the Incrementalist configuration from the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the configuration file. If null, will use the default name in the current directory.</param>
        /// <param name="config">The loaded configuration, or null if the file doesn't exist or couldn't be parsed.</param>
        /// <returns>True if the configuration was loaded successfully, false otherwise.</returns>
        public static bool TryLoad(string? filePath, out IncrementalistConfig? config)
        {
            config = null;
            //filePath ??= DefaultConfigFileName;

            // If filePath is not provided, construct the default path inside the .incrementalist directory
            if (string.IsNullOrEmpty(filePath))
            {
                // Use the current working directory if not specified in options (this function doesn't have SlnOptions)
                var workingDir = Directory.GetCurrentDirectory(); 
                filePath = Path.Combine(workingDir, IncrementalistDirectory, DefaultConfigFileName);
            }

            try
            {
                if (!File.Exists(filePath))
                    return false;

                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                config = JsonSerializer.Deserialize<IncrementalistConfig>(json, options);
                return true;
            }
            catch (Exception)
            {
                // Silently fail to load - config file is optional
                return false;
            }
        }
    }
} 
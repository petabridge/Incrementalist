// -----------------------------------------------------------------------
// <copyright file="ConfigMerger.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2023 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Incrementalist.Cmd.Config
{
    /// <summary>
    /// Helper class for merging and processing configuration settings.
    /// </summary>
    public static class ConfigMerger
    {
        /// <summary>
        /// Merges command-line options with configuration file settings.
        /// Command-line options take precedence over configuration file values.
        /// </summary>
        /// <param name="options">The command-line options.</param>
        /// <param name="config">The configuration file settings.</param>
        /// <returns>A new SlnOptions instance with the merged values.</returns>
        public static SlnOptions Merge(SlnOptions options, IncrementalistConfig? config)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (config == null)
                return options;

            // Clone the current options
            var merged = new SlnOptions
            {
                DotNetArgs = options.DotNetArgs,
                RunCommand = options.RunCommand,
                ConfigFile = options.ConfigFile
            };

            // Merge string properties (CLI takes precedence)
            merged.SolutionFilePath = options.SolutionFilePath ?? config.SolutionFilePath;
            merged.OutputFile = options.OutputFile ?? config.OutputFile;
            merged.GitBranch = (options.GitBranch ?? config.GitBranch) ?? "dev";
            merged.WorkingDirectory = options.WorkingDirectory ?? config.WorkingDirectory;

            // Merge bool properties (CLI takes precedence)
            merged.ListFolders = config.ListFolders.GetValueOrDefault(false);
            merged.Verbose = config.Verbose.GetValueOrDefault(false);
            merged.ContinueOnError = config.ContinueOnError.GetValueOrDefault(true);
            merged.RunInParallel = config.RunInParallel.GetValueOrDefault(false);
            merged.FailOnNoProjects = config.FailOnNoProjects.GetValueOrDefault(false);
            
            // Caching is disabled until we redesign it: https://github.com/petabridge/Incrementalist/issues/350
            merged.NoCache = true; //config.NoCache.GetValueOrDefault(false);

            // Merge int properties (CLI takes precedence)
            merged.TimeoutMinutes = config.TimeoutMinutes.GetValueOrDefault(2);

            // Override with any non-default CLI values
            if (options.ListFolders) merged.ListFolders = true;
            if (options.Verbose) merged.Verbose = true;
            if (!options.ContinueOnError) merged.ContinueOnError = false;
            if (options.RunInParallel) merged.RunInParallel = true;
            if (options.FailOnNoProjects) merged.FailOnNoProjects = true;
            if (options.NoCache) merged.NoCache = true;
            if (options.TimeoutMinutes != 2) merged.TimeoutMinutes = options.TimeoutMinutes;

            return merged;
        }

        /// <summary>
        /// Apply default values to any null properties.
        /// </summary>
        /// <param name="options">The options to apply defaults to.</param>
        /// <returns>The same instance with defaults applied.</returns>
        public static SlnOptions ApplyDefaults(SlnOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Default values for string properties
            options.GitBranch ??= "dev";
            
            // Default values for int properties
            if (options.TimeoutMinutes == 0)
                options.TimeoutMinutes = 2;
                
            // Default values for bool properties
            // These are already initialized to their default values by C#
            // but we'll set them explicitly for clarity
            if (!options.ContinueOnError) // Default is true
                options.ContinueOnError = true;

            return options;
        }
    }
} 
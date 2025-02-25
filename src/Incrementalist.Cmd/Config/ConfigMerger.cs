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
        public static SlnOptions Merge(SlnOptions options, IncrementalistConfig config)
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
            merged.GitBranch = options.GitBranch ?? config.GitBranch;
            merged.WorkingDirectory = options.WorkingDirectory ?? config.WorkingDirectory;

            // Merge nullable bool properties (CLI takes precedence)
            merged.ListFolders = options.ListFolders ?? config.ListFolders;
            merged.Verbose = options.Verbose ?? config.Verbose;
            merged.ContinueOnError = options.ContinueOnError ?? config.ContinueOnError;
            merged.RunInParallel = options.RunInParallel ?? config.RunInParallel;
            merged.FailOnNoProjects = options.FailOnNoProjects ?? config.FailOnNoProjects;
            merged.NoCache = options.NoCache ?? config.NoCache;

            // Merge nullable int properties (CLI takes precedence)
            merged.TimeoutMinutes = options.TimeoutMinutes ?? config.TimeoutMinutes;

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

            // Default values
            options.GitBranch ??= "dev";
            options.TimeoutMinutes ??= 2;
            options.ListFolders ??= false;
            options.Verbose ??= false;
            options.ContinueOnError ??= true;
            options.RunInParallel ??= false;
            options.FailOnNoProjects ??= false;
            options.NoCache ??= false;

            return options;
        }
    }
} 
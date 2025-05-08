// -----------------------------------------------------------------------
// <copyright file="BuildSettings.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Extensions.Logging;

namespace Incrementalist
{
    /// <summary>
    ///     The settings used for this execution of incremental build analysis.
    /// </summary>
    public sealed class BuildSettings
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(100);

        public BuildSettings(string targetBranch, RelativePath solutionFile, AbsolutePath workingDirectory,
            IReadOnlyList<string> skipGlobs, IReadOnlyList<string> targetGlobs, TimeSpan? timeoutDuration = null)
        {
            TargetBranch = targetBranch;
            SolutionFile = solutionFile;
            WorkingDirectory = workingDirectory;
            SkipGlobs = skipGlobs;
            TargetGlobs = targetGlobs;
            TimeoutDuration = timeoutDuration ?? DefaultTimeout;
        }

        /// <summary>
        ///     The target branch to compare the current Git HEAD against,
        ///     i.e. the `dev` or `master` branch of the repository.
        /// </summary>
        public string TargetBranch { get; }

        /// <summary>
        ///     The current solution file for us to analyze inside this repository.
        /// </summary>
        /// <remarks>
        /// Relative to the root of <see cref="WorkingDirectory"/>
        /// </remarks>
        public RelativePath SolutionFile { get; }

        /// <summary>
        ///     The folder Incrementalist will be working from.
        /// </summary>
        public AbsolutePath WorkingDirectory { get; }

        /// <summary>
        ///     The length of time we're going to allow this Incrementalist operation to run
        ///     prior to cancelling it.
        /// </summary>
        public TimeSpan TimeoutDuration { get; }

        /// <summary>
        /// Globs to skip when searching for project files.
        /// </summary>
        public IReadOnlyList<string> SkipGlobs { get; }

        /// <summary>
        /// Exclude all projects that don't match the given globs.
        /// </summary>
        public IReadOnlyList<string> TargetGlobs { get; }
    }
}
// -----------------------------------------------------------------------
// <copyright file="BuildSettings.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics.Contracts;
using Microsoft.Extensions.Logging;

namespace Incrementalist
{
    /// <summary>
    ///     The settings used for this execution of incremental build analysis.
    /// </summary>
    public class BuildSettings
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(1);

        public BuildSettings(string targetBranch, RelativePath solutionFile, AbsolutePath workingDirectory,
            TimeSpan? timeoutDuration = null)
        {
            TargetBranch = targetBranch;
            SolutionFile = solutionFile;
            WorkingDirectory = workingDirectory;
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
        ///     When true, ignores any existing cache file and performs a full Roslyn analysis.
        /// </summary>
        public bool NoCache { get; } = true;
    }
}
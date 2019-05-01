using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Incrementalist
{
    /// <summary>
    /// The settings used for this execution of incremental build analysis.
    /// </summary>
    public class BuildSettings
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(1);

        public BuildSettings(string targetBranch, string solutionFile, string workingDirectory, TimeSpan? timeoutDuration = null)
        {
            Contract.Requires(targetBranch != null);
            Contract.Requires(solutionFile != null);
            TargetBranch = targetBranch;
            SolutionFile = solutionFile;
            WorkingDirectory = workingDirectory;
            TimeoutDuration = timeoutDuration ?? DefaultTimeout;
        }

        /// <summary>
        /// The target branch to compare the current Git HEAD against,
        /// i.e. the `dev` or `master` branch of the repository.
        /// </summary>
        public string TargetBranch { get; }

        /// <summary>
        /// The current solution file for us to analyze inside this repository.
        /// </summary>
        public string SolutionFile { get; }

        /// <summary>
        /// The folder Incrementalist will be working from.
        /// </summary>
        public string WorkingDirectory { get; }

        /// <summary>
        /// The length of time we're going to allow this Incrementalist operation to run
        /// prior to cancelling it.
        /// </summary>
        public TimeSpan TimeoutDuration { get; }
    }
}

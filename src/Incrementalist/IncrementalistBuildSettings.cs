using System;
using System.Collections.Generic;

namespace Incrementalist
{
    /// <summary>
    /// The settings used for this execution of incremental build analysis.
    /// </summary>
    public class IncrementalistBuildSettings
    {
        public IncrementalistBuildSettings(string targetBranch, string solutionFile, IReadOnlyList<IBuildCommand> buildSteps)
        {
            TargetBranch = targetBranch;
            SolutionFile = solutionFile;
            BuildSteps = buildSteps;
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
        /// The set of build steps to produce based on the changes in the diff set.
        /// </summary>
        public IReadOnlyList<IBuildCommand> BuildSteps { get; }
    }
}

using System;
using System.Collections.Generic;

namespace Incrementalist
{
    /// <summary>
    /// The settings used for this execution of incremental build analysis.
    /// </summary>
    public class IncrementalistBuildSettings
    {
        public IncrementalistBuildSettings(string targetBranch, string solutionFile, IReadOnlyList<BuildStep> buildSteps)
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
        public IReadOnlyList<BuildStep> BuildSteps { get; }
    }

    /// <summary>
    /// Used to define a build step, which may or may not be used
    /// depending on whether or not one of the affected projects requires it.
    /// </summary>
    public sealed class BuildStep
    {
        public BuildStep(string stepName, string commandText, IBuildStepApplicator applicator)
        {
            StepName = stepName;
            CommandText = commandText;
            Applicator = applicator;
        }

        /// <summary>
        /// The name of this build step.
        /// </summary>
        public string StepName { get; }

        /// <summary>
        /// The text to emit when this build step is used
        /// </summary>
        public string CommandText { get; }

        /// <summary>
        /// The applicator used to evaluate whether or not this build step applies to a particular path.
        /// </summary>
        public IBuildStepApplicator Applicator { get; }
    }


}

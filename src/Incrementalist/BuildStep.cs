namespace Incrementalist
{
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
        /// The text to emit when this build step is used.
        ///
        /// Some supported format strings:
        ///  - "{fileName}" will swap in the name of the file, with the extension
        ///  - "{filePath}" will swap in the absolute path of the file
        ///  - "{projectName}" will swap in the name of the project, without the extension
        ///  - "{projectPath}" will swap in the full path of the project including the extension
        /// </summary>
        public string CommandText { get; }

        /// <summary>
        /// The applicator used to evaluate whether or not this build step applies to a particular path.
        /// </summary>
        public IBuildStepApplicator Applicator { get; }
    }
}
namespace Incrementalist.ProjectSystem
{
    /// <summary>
    /// Run this step for all affected projects
    /// </summary>
    public sealed class AllAffectedProjectsApplicator : IBuildStepApplicator
    {
        public static readonly AllAffectedProjectsApplicator Instance = new AllAffectedProjectsApplicator();
        private AllAffectedProjectsApplicator() { }
        
        public bool ShouldRunForPath(AffectedFile affectedFile)
        {
            return AffectedProjectsHolder.AffectedProjects.Value.Contains(affectedFile.Project);
        }
    }
}
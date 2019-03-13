using System.Collections.Generic;
using static Incrementalist.ProjectSystem.AffectedProjectsHolder;

namespace Incrementalist
{
    /// <summary>
    /// Used to test whether or not a build step applies to a given project
    /// </summary>
    public interface IBuildStepApplicator
    {
        /// <summary>
        /// Indicates if this build step should be used for a given path.
        /// </summary>
        /// <param name="affectedFile">One of the files affected by the git diff..</param>
        /// <returns><c>true</c> if this build step should run for the given path.</returns>
        bool ShouldRunForPath(AffectedFile affectedFile);
    }

    /// <summary>
    /// Run this step for all affected projects
    /// </summary>
    public sealed class AllAffectedProjectsApplicator : IBuildStepApplicator
    {
        public static readonly AllAffectedProjectsApplicator Instance = new AllAffectedProjectsApplicator();
        private AllAffectedProjectsApplicator() { }
        
        public bool ShouldRunForPath(AffectedFile affectedFile)
        {
            return AffectedProjects.Value.Contains(affectedFile.Project);
        }
    }
}
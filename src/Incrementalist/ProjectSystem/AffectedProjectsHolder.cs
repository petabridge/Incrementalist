using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Incrementalist.ProjectSystem
{
    /// <summary>
    /// INTERNAL API
    ///
    /// Used to hold the set of currently affected projects for the benefit of the <see cref="AllAffectedProjectsApplicator"/>,
    /// since at configuration time it is not known which changes affect which projects.
    /// </summary>
    internal static class AffectedProjectsHolder
    {
        public static ThreadLocal<HashSet<string>> AffectedProjects { get; } = new ThreadLocal<HashSet<string>>(() => new HashSet<string>());
    }
}
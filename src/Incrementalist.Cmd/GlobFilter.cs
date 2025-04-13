using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Incrementalist.Cmd;

public static class GlobFilter
{
    /// <summary>
    /// After Incrementalist has done its processing, we do some post-processing here to further narrow down
    /// the range of projects to be processed based on glob patterns.
    /// </summary>
    /// <param name="originalProjects">The projects determined to need coverage from Incrementalist</param>
    /// <param name="skipGlobs">Filter out any projects that match these glob patterns.</param>
    /// <param name="targetGlobs">Only include projects that match these glob patterns.</param>
    /// <returns>The final set of filtered project paths.</returns>
    public static IReadOnlyList<string> FilterProjects(IReadOnlyList<string> originalProjects, string[] skipGlobs, string[] targetGlobs)
    {
        if (skipGlobs.Length == 0 && targetGlobs.Length == 0)
            return originalProjects;

        IEnumerable<string> currentProjects = originalProjects;

        // 1. Apply targetGlobs (inclusion filter)
        if (targetGlobs.Length > 0)
        {
            var targetMatcher = new Matcher(StringComparison.OrdinalIgnoreCase); // Use case-insensitive matching for file paths
            targetMatcher.AddIncludePatterns(targetGlobs);
            // Keep only projects that match at least one target pattern
            currentProjects = currentProjects.Where(p => targetMatcher.Match(p).HasMatches);
        }

        // 2. Apply skipGlobs (exclusion filter)
        if (skipGlobs.Length > 0)
        {
            var skipMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            skipMatcher.AddIncludePatterns(skipGlobs); // Add skip patterns to identify matches for exclusion
            // Keep only projects that *do not* match any skip pattern
            currentProjects = currentProjects.Where(p => !skipMatcher.Match(p).HasMatches);
        }

        // Return the result as a List (which implements IReadOnlyList)
        return currentProjects.ToList();
    }
}
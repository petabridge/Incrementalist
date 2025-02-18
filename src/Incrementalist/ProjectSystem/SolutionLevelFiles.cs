using System.Collections.Generic;
using System.IO;

namespace Incrementalist.ProjectSystem
{
    /// <summary>
    /// Defines and manages solution-level files that affect the entire solution build
    /// </summary>
    public static class SolutionLevelFiles
    {
        /// <summary>
        /// Default set of file names that are considered solution-level and trigger full solution builds
        /// </summary>
        public static readonly HashSet<string> DefaultSolutionLevelFiles = new()
        {
            "Directory.Build.props",
            "Directory.Build.targets",
            "Directory.Packages.props",
            "global.json",
            "NuGet.config",
            ".editorconfig"
        };

        /// <summary>
        /// Checks if a file path represents a solution-level file
        /// </summary>
        public static bool IsSolutionLevelFile(string filePath, HashSet<string> solutionLevelFiles = null)
        {
            var fileName = Path.GetFileName(filePath);
            
            // Check if it's a solution file first
            if (Path.GetExtension(fileName).Equals(".sln", System.StringComparison.OrdinalIgnoreCase))
                return true;

            // Then check against the configured set of solution-level files
            return (solutionLevelFiles ?? DefaultSolutionLevelFiles).Contains(fileName);
        }
    }
} 
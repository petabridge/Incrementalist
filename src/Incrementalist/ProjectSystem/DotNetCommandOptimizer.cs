using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Incrementalist.ProjectSystem
{
    /// <summary>
    /// Optimizes dotnet command execution based on affected files and project dependencies
    /// </summary>
    public class DotNetCommandOptimizer
    {
        /// <summary>
        /// Commands that support multiple project inputs
        /// </summary>
        private static readonly HashSet<string> MultiProjectCommands = new()
        {
            "build",
            "clean",
            "restore"
        };

        /// <summary>
        /// Commands that require individual project execution
        /// </summary>
        private static readonly HashSet<string> SingleProjectCommands = new()
        {
            "test",
            "pack",
            "publish"
        };

        /// <summary>
        /// Determines if a command needs to be run on the entire solution
        /// </summary>
        public bool ShouldRunOnFullSolution(IEnumerable<string> affectedFiles)
        {
            return affectedFiles.Any(file => SolutionLevelFiles.IsSolutionLevelFile(file));
        }

        /// <summary>
        /// Optimizes the dotnet command execution by determining whether to run on solution or individual projects
        /// </summary>
        /// <param name="command">The dotnet command to run (e.g. "build", "test")</param>
        /// <param name="affectedProjects">List of affected project files</param>
        /// <param name="solutionFile">Path to the solution file</param>
        /// <returns>A list of command arguments to execute</returns>
        public IEnumerable<string> OptimizeCommand(string command, IEnumerable<string> affectedProjects, string solutionFile)
        {
            // Extract the base command (e.g. "build" from "build -c Release")
            var baseCommand = command.Split(' ')[0].ToLowerInvariant();

            // For build command, we can optimize by building all projects at once
            // as dependencies will be built automatically
            if (baseCommand == "build")
            {
                yield return $"{command} {string.Join(" ", affectedProjects)}";
                yield break;
            }

            // For all other commands, run them individually
            foreach (var project in affectedProjects)
            {
                yield return $"{command} {project}";
            }
        }
    }
} 
// -----------------------------------------------------------------------
// <copyright file="SolutionFinder.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;

namespace Incrementalist.ProjectSystem
{
    /// <summary>
    ///     Used to look for .sln files in a given directory.
    /// </summary>
    public static class SolutionFinder
    {
        /// <summary>
        ///     The default filter used to look for solutions in a given folder.
        /// </summary>
        public const string DefaultSolutionFilter = "*.sln";

        /// <summary>
        ///     Enumerate all of the MSBuild solution files in a given folder.
        /// </summary>
        /// <param name="folderPath">The top level path to search.</param>
        /// <param name="searchFilter">Optional. A wildcard filter in the form of "*.sln".</param>
        /// <param name="searchOption">Optional. Specifies whether to recurse sub-directories or not.</param>
        /// <returns>If any solutions are found, will return an enumerable list of them in order in which they are discovered.</returns>
        public static IEnumerable<string> GetSolutions(string folderPath, string searchFilter = null,
            SearchOption? searchOption = null)
        {
            if (string.IsNullOrEmpty(searchFilter))
                return Directory.EnumerateFileSystemEntries(folderPath, DefaultSolutionFilter,
                    searchOption ?? SearchOption.AllDirectories);

            return Directory.EnumerateFileSystemEntries(folderPath, searchFilter,
                searchOption ?? SearchOption.AllDirectories);
        }
    }
}
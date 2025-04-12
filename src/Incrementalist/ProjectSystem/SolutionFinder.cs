// -----------------------------------------------------------------------
// <copyright file="SolutionFinder.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Incrementalist.ProjectSystem
{
    /// <summary>
    ///     Used to look for .sln and .slnx files in a given directory.
    /// </summary>
    public static class SolutionFinder
    {
        /// <summary>
        ///     Enumerate all of the MSBuild solution files (.sln and .slnx) in a given folder.
        /// </summary>
        /// <param name="folderPath">The top level path to search.</param>
        /// <param name="searchFilter">Optional. A wildcard filter, e.g., "*.sln". If null or empty, defaults to searching for both "*.sln" and "*.slnx".</param>
        /// <param name="searchOption">Optional. Specifies whether to recurse sub-directories or not. Defaults to <see cref="SearchOption.AllDirectories"/>.</param>
        /// <returns>If any solutions are found, will return an enumerable list of their paths, ordered by filename.</returns>
        public static IEnumerable<string> GetSolutions(string folderPath, string searchFilter = null,
            SearchOption? searchOption = null)
        {
            var finalSearchOption = searchOption ?? SearchOption.AllDirectories;

            if (string.IsNullOrEmpty(searchFilter))
            {
                // Search for both .sln and .slnx if no specific filter is provided
                var slnFiles = Directory.EnumerateFileSystemEntries(folderPath, "*.sln", finalSearchOption);
                var slnxFiles = Directory.EnumerateFileSystemEntries(folderPath, "*.slnx", finalSearchOption);
                return slnFiles.Concat(slnxFiles).OrderBy(Path.GetFileName);
            }
            
            // Use the provided search filter
            return Directory.EnumerateFileSystemEntries(folderPath, searchFilter, finalSearchOption)
                .OrderBy(Path.GetFileName);
        }
    }
}
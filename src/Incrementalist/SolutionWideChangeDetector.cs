// -----------------------------------------------------------------------
// <copyright file="SolutionWideChangeDetector.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Incrementalist
{
    /// <summary>
    /// Detects changes that would require a full solution build rather than an incremental build.
    /// </summary>
    public sealed class SolutionWideChangeDetector
    {
        private static readonly HashSet<string> SolutionWideFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Directory.Build.props",
            "Directory.Packages.props",
            "global.json",
            "nuget.config"
        };

        private static readonly HashSet<string> SolutionWideExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".sln",
            ".props",
            ".targets"
        };

        /// <summary>
        /// Determines if any of the changed files would require a full solution build.
        /// </summary>
        /// <param name="changedFiles">The list of files that have changed.</param>
        /// <returns>True if a full solution build is required, false otherwise.</returns>
        public bool RequiresFullSolutionBuild(IEnumerable<string> changedFiles)
        {
            if (changedFiles == null) throw new ArgumentNullException(nameof(changedFiles));

            return changedFiles.Any(file =>
            {
                var fileName = Path.GetFileName(file);
                var extension = Path.GetExtension(file);

                return SolutionWideFileNames.Contains(fileName) || 
                       SolutionWideExtensions.Contains(extension);
            });
        }
    }
} 
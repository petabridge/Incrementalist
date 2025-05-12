// -----------------------------------------------------------------------
// <copyright file="SolutionWideChangeDetector.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Incrementalist.ProjectSystem;

namespace Incrementalist
{
    /// <summary>
    /// Detects changes that would require a full solution build rather than an incremental build.
    /// </summary>
    public sealed class SolutionWideChangeDetector
    {
        private static readonly HashSet<string> AlwaysSolutionWideFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Directory.Build.props",
            "Directory.Packages.props",
            "global.json",
            "nuget.config"
        };

        private static readonly HashSet<string> AlwaysSolutionWideExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".sln"
        };

        private readonly IReadOnlyDictionary<AbsolutePath, ImportedFile> _projectImports;

        /// <summary>
        /// Creates a new instance of the SolutionWideChangeDetector using a Solution object.
        /// </summary>
        public SolutionWideChangeDetector(Solution solution)
        {
            ArgumentNullException.ThrowIfNull(solution);
            ArgumentException.ThrowIfNullOrEmpty(solution.FilePath.Path);

            var projectFiles = solution.Projects
                .Select(p => new SlnFileWithPath(p.FilePath, new SlnFile(FileType.Project, p)))
                .ToList();

            _projectImports = ProjectImportsFinder.FindProjectImports(projectFiles);
        }

        /// <summary>
        /// Creates a new instance of the SolutionWideChangeDetector using pre-computed project imports.
        /// </summary>
        public SolutionWideChangeDetector(IReadOnlyDictionary<AbsolutePath, ImportedFile> projectImports)
        {
            _projectImports = projectImports ?? throw new ArgumentNullException(nameof(projectImports));
        }

        /// <summary>
        /// Determines if any of the changed files would require a full solution build.
        /// </summary>
        /// <param name="changedFiles">The list of files that have changed.</param>
        /// <returns>True if a full solution build is required, false otherwise.</returns>
        public bool RequiresFullSolutionBuild(IEnumerable<AbsolutePath> changedFiles)
        {
            if (changedFiles == null) throw new ArgumentNullException(nameof(changedFiles));

            foreach (var file in changedFiles)
            {
                if (IsSolutionWideFile(file))
                    return true;

                // If it's a .props or .targets file, check if it affects multiple projects
                if (_projectImports.TryGetValue(file, out var importedFile))
                {
                    if (IsWidelyImportedFile(importedFile))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a file is considered solution-wide based on its name or extension.
        /// </summary>
        internal static bool IsSolutionWideFile(AbsolutePath filePath)
        {
            var fileName = Path.GetFileName(filePath.Path);
            var extension = Path.GetExtension(filePath.Path);

            return AlwaysSolutionWideFiles.Contains(fileName) ||
                   AlwaysSolutionWideExtensions.Contains(extension);
        }

        /// <summary>
        /// Determines if an imported file affects multiple projects or is imported by Directory.Build.props.
        /// </summary>
        internal static bool IsWidelyImportedFile(ImportedFile importedFile)
        {
            // If this props/targets file is imported by Directory.Build.props or affects multiple projects,
            // we should do a full solution build
            var isImportedByDirectoryBuildProps = importedFile.DependentProjects
                .Any(p => Path.GetFileName(p.Path.Path)
                    .Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase));

            var affectsMultipleProjects = importedFile.DependentProjects.Count > 1;

            return isImportedByDirectoryBuildProps || affectsMultipleProjects;
        }

        /// <summary>
        /// Creates the appropriate BuildAnalysisResult based on whether all projects in the solution are affected.
        /// </summary>
        /// <param name="solution">The solution being analyzed.</param>
        /// <param name="affectedProjects">The list of affected project paths.</param>
        /// <returns>A FullSolutionBuildResult if all projects are affected, otherwise an IncrementalBuildResult.</returns>
        public static BuildAnalysisResult CreateBuildResult(Solution solution,
            IReadOnlyList<AbsolutePath> affectedProjects)
        {
            ArgumentNullException.ThrowIfNull(solution);
            ArgumentNullException.ThrowIfNull(affectedProjects);
            ArgumentException.ThrowIfNullOrEmpty(solution.FilePath.Path);

            var totalProjects = solution.Projects.Count;

            // If all projects are affected, return a full solution build result
            if (affectedProjects.Count == totalProjects)
            {
                return new FullSolutionBuildResult(solution.FilePath);
            }

            return new IncrementalBuildResult(affectedProjects);
        }
    }
}
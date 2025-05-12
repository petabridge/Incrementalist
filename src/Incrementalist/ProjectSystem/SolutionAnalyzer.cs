// -----------------------------------------------------------------------
// <copyright file="SolutionAnalyzer.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Incrementalist.ProjectSystem
{
    public readonly struct SlnFile
    {
        public SlnFile(FileType fileType, Project? project)
        {
            FileType = fileType;
            Project = project;
        }

        public FileType FileType { get; }

        /// <summary>
        ///     The project to which this file belongs.
        ///     Used in the topological sorting of dependencies later.
        /// </summary>
        /// <remarks>
        ///     Will be <c>null</c> when <see cref="FileType" /> is Solution file.
        /// </remarks>
        public Project? Project { get; }
    }

    /// <summary>
    ///     Analyzes MSBuild solutions using the Roslyn Workspaces API
    /// </summary>
    public static class SolutionAnalyzer
    {
        /// <summary>
        ///     Produces a flat, unique list of all files in the solution, including .csproj and .sln files.
        /// </summary>
        /// <param name="sln">The Solution file.</param>
        /// <param name="workingFolder"></param>
        /// <returns>A flattened list of all files inside the solution.</returns>
        public static Dictionary<AbsolutePath, SlnFile[]> AllSolutionFiles(Solution sln, AbsolutePath workingFolder)
        {
            // throw if the solution's file path is null
            ArgumentNullException.ThrowIfNull(sln.FilePath, nameof(sln.FilePath));

            var allPossibleFiles = sln.Projects.SelectMany(x => x.Documents)
                .GroupBy(x => x.FilePath, document => new SlnFile(document.FileType, document.Project))
                .ToDictionary(x => x.Key, x => x.ToArray()).ToList()
                .Concat(sln.Projects.Select(x =>
                        new KeyValuePair<AbsolutePath, SlnFile[]>(x.FilePath, [new SlnFile(FileType.Project, x)]))
                    .Append(
                        new KeyValuePair<AbsolutePath, SlnFile[]>(sln.FilePath, [new SlnFile(FileType.Solution, null)])
                    ));

            // need to de-duplicate
            var finalFiles = new Dictionary<AbsolutePath, SlnFile[]>();
            foreach (var file in allPossibleFiles)
            {
                finalFiles[file.Key] = file.Value;
            }

            return finalFiles;
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="SolutionAnalyzer.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Incrementalist.ProjectSystem
{
    public struct SlnFile
    {
        public SlnFile(FileType fileType, ProjectId? projectId)
        {
            FileType = fileType;
            ProjectId = projectId;
        }

        public FileType FileType { get; }

        /// <summary>
        ///     The ID of the project to which this file belongs.
        ///     Used in the topological sorting of dependencies later.
        /// </summary>
        /// <remarks>
        ///     Will be <c>null</c> when <see cref="FileType" /> is Solution file.
        /// </remarks>
        public ProjectId? ProjectId { get; }
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
        public static Dictionary<string, SlnFile> AllSolutionFiles(Solution sln, string workingFolder)
        {
            ArgumentNullException.ThrowIfNull(sln);
            ArgumentNullException.ThrowIfNull(workingFolder);

            // Get all document files
            var documentFiles = sln.Projects
                .SelectMany(x => x.Documents)
                .Where(x => x.FilePath != null)
                .Select(x => new
                {
                    Path = x.FilePath!,  // We know it's not null from the Where clause
                    SlnFile = new SlnFile(
                        x.SourceCodeKind == SourceCodeKind.Regular ? FileType.Code : FileType.Script,
                        x.Project.Id)
                })
                .ToDictionary(x => Path.GetFullPath(x.Path), x => x.SlnFile);

            // Get all project files
            var projectFiles = sln.Projects
                .Where(x => x.FilePath != null)
                .Select(x => new KeyValuePair<string, SlnFile>(
                    Path.GetFullPath(x.FilePath!),  // We know it's not null from the Where clause
                    new SlnFile(FileType.Project, x.Id)));

            // Get solution file if it exists
            var solutionFile = sln.FilePath != null
                ? new[] { new KeyValuePair<string, SlnFile>(
                    Path.GetFullPath(sln.FilePath),
                    new SlnFile(FileType.Solution, null)) }
                : Array.Empty<KeyValuePair<string, SlnFile>>();

            // Combine all files and de-duplicate
            var finalFiles = new Dictionary<string, SlnFile>();

            // Add document files
            foreach (var file in documentFiles)
            {
                finalFiles[file.Key] = file.Value;
            }

            // Add project files
            foreach (var file in projectFiles)
            {
                finalFiles[file.Key] = file.Value;
            }

            // Add solution file if it exists
            foreach (var file in solutionFile)
            {
                finalFiles[file.Key] = file.Value;
            }

            return finalFiles;
        }
    }
}
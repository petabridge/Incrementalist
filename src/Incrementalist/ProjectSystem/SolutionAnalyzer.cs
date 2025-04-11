// -----------------------------------------------------------------------
// <copyright file="SolutionAnalyzer.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Incrementalist.ProjectSystem.Cmds;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Incrementalist.ProjectSystem
{
    public readonly struct SlnFile
    {
        public SlnFile(FileType fileType, Guid? projectId)
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
        public Guid? ProjectId { get; }
    }

    /// <summary>
    ///     Analyzes MSBuild solutions using the Roslyn Workspaces API
    /// </summary>
    public static class SolutionAnalyzer
    {
        public static FileType ClassifyFileTypeByExtension(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return ext switch
            {
                ".cs" => FileType.Code,
                ".vb" => FileType.Code,
                ".fs" => FileType.Code,
                ".csx" => FileType.Script,
                ".fsx" => FileType.Script,
                ".js" => FileType.Script,
                ".ts" => FileType.Script,
                ".json" => FileType.Script,
                ".xml" => FileType.Script,
                ".sln" => FileType.Solution,
                ".slnx" => FileType.Solution,
                ".csproj" => FileType.Project,
                ".vbproj" => FileType.Project,
                ".fsproj" => FileType.Project,
                _ => FileType.Other
            };
        }

        /// <summary>
        ///     Produces a flat, unique list of all files in the solution, including .csproj and .sln files.
        /// </summary>
        /// <param name="sln">The Solution file.</param>
        /// <param name="workingFolder"></param>
        /// <returns>A flattened list of all files inside the solution.</returns>
        public static Dictionary<string, SlnFile> AllSolutionFiles(SolutionDetails sln, string workingFolder)
        {
            // throw if the solution's file path is null
            ArgumentNullException.ThrowIfNull(sln.SolutionFilePath, nameof(sln.SolutionFilePath));

            var slnModel = sln.SolutionModel;

            var allProjectFiles = slnModel.SolutionProjects
                .Select(x => (x, Path.GetDirectoryName(x.FilePath)))
                .SelectMany(c => Directory.GetFiles(c.Item2!, "*", SearchOption.AllDirectories).Select(f =>
                    new KeyValuePair<string, SlnFile>(Path.GetFullPath(f),
                        new SlnFile(ClassifyFileTypeByExtension(f), c.x.Id))));
            
            // need to scan for files in the same directory as the solution
            var slnDir = Path.GetDirectoryName(sln.SolutionFilePath);
            if (slnDir == null)
                throw new DirectoryNotFoundException($"Could not find directory for solution file {sln.SolutionFilePath}");

            var slnFiles = Directory.GetFiles(slnDir, "*", SearchOption.AllDirectories)
                .Select(f => new KeyValuePair<string, SlnFile>(Path.GetFullPath(f),
                    new SlnFile(ClassifyFileTypeByExtension(f), null)));
            allProjectFiles = allProjectFiles.Concat(slnFiles);

            // need to de-duplicate
            var finalFiles = new Dictionary<string, SlnFile>();
            foreach (var file in allProjectFiles)
            {
                finalFiles[file.Key] = file.Value;
            }

            return finalFiles;
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="FilterAffectedProjectFilesCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Git;
using Microsoft.Extensions.Logging;

namespace Incrementalist.ProjectSystem.Cmds
{
    /// <summary>
    ///     Filter the previously discovered <see cref="SlnFile" />s by whether
    ///     they were touched via the GitDiff.
    /// </summary>
    public sealed class
        FilterAffectedProjectFilesCmd : BuildCommandBase<Dictionary<AbsolutePath, SlnFile[]>,
        Dictionary<AbsolutePath, SlnFile[]>>
    {
        private readonly string _targetGitBranch;
        private readonly AbsolutePath _workingDirectory;

        public FilterAffectedProjectFilesCmd(ILogger logger, CancellationToken cancellationToken,
            AbsolutePath workingDirectory, string targetGitBranch)
            : base("FilterSlnFilesByGitDiff", logger, cancellationToken)
        {
            _workingDirectory = workingDirectory;
            _targetGitBranch = targetGitBranch;
        }

        protected override async Task<Dictionary<AbsolutePath, SlnFile[]>> ProcessImpl(
            Task<Dictionary<AbsolutePath, SlnFile[]>> previousTask)
        {
            var fileDictObj = await previousTask;

            var (repo, foundRepo) = GitRunner.FindRepository(_workingDirectory);
            if (!foundRepo || repo == null)
            {
                Logger.LogError("Unable to find Git repository located in {WorkingDirectory}. Shutting down.",
                    _workingDirectory);
                return [];
            }

            // validate the target branch
            if (!DiffHelper.HasBranch(repo, _targetGitBranch))
            {
                Logger.LogError("Current git repository doesn't have any branch named [{TargetBranch}]. Shutting down.",
                    _targetGitBranch);
                return [];
            }

            var affectedFiles = DiffHelper.ChangedFiles(repo, _targetGitBranch).ToList();

            var projectFiles = fileDictObj.Where(x => x.Value is [{ FileType: FileType.Project }]).ToList();
            var projectFolders = projectFiles.Where(x => Path.GetDirectoryName(x.Key.Path) is not null)
                .ToLookup(x => new AbsolutePath(Path.GetDirectoryName(x.Key.Path)!), v => Tuple.Create(v.Key, v.Value));
            var projectImports =
                ProjectImportsFinder.FindProjectImports(projectFiles.Select(pair =>
                    new SlnFileWithPath(pair.Key, pair.Value[0])));

            // filter out any files that aren't affected by the diff
            var newDict = new Dictionary<AbsolutePath, SlnFile[]>();
            foreach (var file in affectedFiles)
            {
                Logger.LogDebug("Affected file: {FilePath}", file);

                var foundInSolution = fileDictObj.TryGetValue(file, out var value);
                if (foundInSolution)
                {
                    newDict[file] = value!;
                }
                else
                {
                    // Need to see if this file is a solution-wide file
                    if (SolutionWideChangeDetector.IsSolutionWideFile(file))
                    {
                        Logger.LogInformation("Adding solution-wide file {File} to the set of affected files.", file);
                        newDict[file] = [new SlnFile(FileType.Other, null)];
                        continue;
                    }
                }

                /*
                 * Containment supplements an exact match only for NON-SOURCE files.
                 *
                 * An exact document match identifies the project that *registers* the file, which is
                 * not necessarily every project that *consumes* it. A file can be registered as an
                 * AdditionalFile by one project while another project owns it as content or a
                 * resource. Skipping containment there would drop the consuming project.
                 *
                 * Source files are different: a .cs file has exactly one owning project, and the exact
                 * match already found it. Running containment for them would additionally select every
                 * ancestor project directory, which is wrong -- a nested project's source file does not
                 * affect the outer project.
                 */
                var isSourceFile = foundInSolution &&
                                   value!.Any(f => f.FileType is FileType.Code or FileType.Script);

                if (!isSourceFile && TryFindContainingProjects(projectFolders.Select(c => c.Key), file,
                        out var containingProjects))
                {
                    foreach (var projectFolder in containingProjects)
                    {
                        var affectedProjects = projectFolders[projectFolder];
                        foreach (var (projectPath, project) in affectedProjects)
                        {
                            if (foundInSolution)
                            {
                                Logger.LogInformation(
                                    "Adding project {0} to the set of affected files because file {1}, " +
                                    "found inside project directory [{2}], was modified.", projectPath, file,
                                    projectFolder);
                            }
                            else
                            {
                                Logger.LogInformation(
                                    "Adding project {0} to the set of affected files because non-code file {1}, " +
                                    "found inside project directory [{2}], was modified.", projectPath, file,
                                    projectFolder);
                            }

                            newDict[projectPath] = project;
                        }
                    }
                }

                // special case - if affected file was imported to some projects, need to mark importing project as affected
                if (projectImports.TryGetValue(file, value: out var import))
                {
                    // Mark all dependant as affected
                    foreach (var dependentProject in import.DependentProjects)
                    {
                        newDict[dependentProject.Path] = [dependentProject.File];
                    }
                }
            }

            return newDict;
        }

        /// <summary>
        ///     Finds every project directory that lexically contains <paramref name="file" />.
        /// </summary>
        /// <remarks>
        ///     Containment is segment-aware and purely lexical: the file does not need to exist, which
        ///     matters for deletions. A file directly inside a project directory counts, as does one
        ///     nested arbitrarily deep. When projects are nested, every containing project is returned
        ///     rather than just the first match, so a file under <c>src/Outer/Inner/</c> attributes to
        ///     both <c>Outer</c> and <c>Inner</c> instead of silently picking one.
        /// </remarks>
        internal static bool TryFindContainingProjects(IEnumerable<AbsolutePath> projectFolders, AbsolutePath file,
            [NotNullWhen(true)] out IReadOnlyList<AbsolutePath>? containingProjects)
        {
            containingProjects = null;
            var matches = new List<AbsolutePath>();

            foreach (var projectFolder in projectFolders)
            {
                if (IsUnderDirectory(file.Path, projectFolder.Path))
                    matches.Add(projectFolder);
            }

            if (matches.Count == 0)
                return false;

            containingProjects = matches;
            return true;
        }

        /// <summary>
        ///     Returns <c>true</c> when <paramref name="filePath" /> is the same as, or nested beneath,
        ///     <paramref name="directoryPath" />.
        /// </summary>
        /// <remarks>
        ///     Compares path segments rather than raw string prefixes so that <c>src/CoreExtra</c> is not
        ///     treated as being under <c>src/Core</c>. Trailing separators are normalized away first.
        /// </remarks>
        internal static bool IsUnderDirectory(string filePath, string directoryPath)
        {
            var normalizedFile = Path.GetFullPath(filePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedDirectory = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (normalizedFile.Equals(normalizedDirectory, PathComparison))
                return true;

            // Require the separator so that a sibling with a shared prefix does not match.
            var prefix = normalizedDirectory + Path.DirectorySeparatorChar;
            return normalizedFile.StartsWith(prefix, PathComparison);
        }

        /// <summary>
        ///     Path comparison appropriate for the current platform. Windows and macOS are
        ///     case-insensitive by default; Linux is case-sensitive.
        /// </summary>
        private static StringComparison PathComparison =>
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
    }
}
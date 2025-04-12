// -----------------------------------------------------------------------
// <copyright file="DependencyCache.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.ProjectSystem;
using Incrementalist.ProjectSystem.Cmds;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Caching
{
    /// <summary>
    /// Represents the cached dependency information for a solution
    /// </summary>
    public sealed record DependencyCache(
        string Version,
        string SolutionPath,
        string Checksum,
        ImmutableDictionary<Guid, ProjectNode> Projects);

    /// <summary>
    /// Represents a project and its direct dependencies in the solution
    /// </summary>
    public sealed record ProjectNode(
        Guid Id,
        string Path,
        ImmutableList<Guid> Dependencies);

    // [JsonSourceGenerationOptions(WriteIndented = true)]
    // [JsonSerializable(typeof(DependencyCache))]
    // internal partial class CacheGenerationContext : JsonSerializerContext
    // {
    //     
    // }

    /// <summary>
    /// Handles serialization and deserialization of the dependency cache
    /// </summary>
    public static class DependencyCacheIO
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            Converters =
            {
                new ProjectIdJsonConverter(),
                new SolutionIdJsonConverter()
            }
        };

        public static string GetCachePath(string solutionDir) =>
            Path.Combine(solutionDir, IncrementalistFileConstants.IncrementalistDirectory,
                IncrementalistFileConstants.CacheFileName);

        public static async Task<DependencyCache?> LoadAsync(string path)
        {
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<DependencyCache>(json, SerializerOptions);
        }

        public static async Task SaveAsync(string path, DependencyCache cache)
        {
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(path);

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty or whitespace.", nameof(path));

            var dir = Path.GetDirectoryName(path);

            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(cache, SerializerOptions));
        }
    }

    /// <summary>
    /// Helper methods for creating and manipulating dependency caches
    /// </summary>
    public static class DependencyCacheHelper
    {
        /// <summary>
        /// Validates whether a cache is still valid for the current solution
        /// </summary>
        /// <param name="cache">The cache to validate, or null if no cache exists</param>
        /// <param name="baseRepositoryPath">The base of the repository being analyzed</param>
        /// <param name="solution">The current solution to validate against</param>
        /// <param name="logger">Optional logger for diagnostic information</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the cache is valid and can be used, false if it needs to be regenerated</returns>
        public static async Task<bool> IsCacheValidAsync(
            DependencyCache? cache,
            string baseRepositoryPath,
            SolutionDetails solution,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(solution);

            // No cache exists
            if (cache is null)
            {
                logger?.LogDebug("No existing cache found");
                return false;
            }

            // Version mismatch
            if (cache.Version != IncrementalistFileConstants.CurrentVersion)
            {
                logger?.LogInformation(
                    "Cache version mismatch. Expected {ExpectedVersion}, found {ActualVersion}",
                    IncrementalistFileConstants.CurrentVersion,
                    cache.Version);
                return false;
            }
            
            // get absolute paths of both solutions relative to the current working directory
            var liveSolutionPath = Path.GetRelativePath(baseRepositoryPath, solution.SolutionFilePath);

            // Solution path mismatch
            if (cache.SolutionPath != liveSolutionPath)
            {
                logger?.LogInformation(
                    "Cache is for different solution. Expected [{ExpectedPath}], found [{ActualPath}]",
                    liveSolutionPath,
                    cache.SolutionPath);
                return false;
            }

            // Calculate current checksum
            var projectPaths = solution.SolutionModel.SolutionProjects
                .Select(p => p.FilePath)
                .ToList();

            var currentChecksum = await ChecksumCalculator.CalculateChecksumAsync(
                solution.SolutionFilePath,
                projectPaths,
                cancellationToken);

            // Checksum mismatch
            if (currentChecksum != cache.Checksum)
            {
                logger?.LogInformation(
                    "Solution or project files have changed. Cache needs to be regenerated");
                return false;
            }

            logger?.LogDebug("Cache is valid");
            return true;
        }

        /// <summary>
        /// Creates a new DependencyCache from a Solution object
        /// </summary>
        /// <param name="repositoryRoot"></param>
        /// <param name="solution">The solution to analyze</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A new DependencyCache instance</returns>
        public static async Task<DependencyCache> CreateFromSolutionAsync(
            string repositoryRoot,
            SolutionDetails solution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(solution);

            // Build the Projects dictionary
            var projectsBuilder = ImmutableDictionary.CreateBuilder<Guid, ProjectNode>();
            foreach (var projectId in solution.SolutionModel.SolutionProjects.Select(c => c.Id))
            {
                var project = solution.GetProject(projectId);
                if (project?.FilePath == null) continue;

                var dependencies = solution
                    .GetProjectsThatThisProjectDirectlyDependsOn(projectId)
                    .Select(solution.GetProject)
                    .Where(p => p != null)
                    .Select(c => c!.Id)
                    .ToImmutableList();

                projectsBuilder.Add(project.Id,
                    new ProjectNode(project.Id, project.FilePath, dependencies));
            }

            // Calculate checksum for all project files
            var projectPaths = solution.SolutionModel.SolutionProjects
                .Select(p => GetAbsolutePathFromRepositoryRoot(p.FilePath))
                .ToList();

            var checksum = await ChecksumCalculator.CalculateChecksumAsync(
                solution.SolutionFilePath,
                projectPaths,
                cancellationToken);

            return new DependencyCache(
                Version: IncrementalistFileConstants.CurrentVersion,
                SolutionPath: GetPathRelativeToRepositoryRoot(solution.SolutionFilePath),
                Checksum: checksum,
                Projects: projectsBuilder.ToImmutable());

            // add a local function to compute a relative file path from the repository root
            // use System.IO Path tools for this, not string manipulation
            string GetPathRelativeToRepositoryRoot(string filePath)
            {
                return GetRelativePath(repositoryRoot, filePath);
            }

            // Using the SolutionModel from https://github.com/microsoft/vs-solutionpersistence,
            // all paths are relative - but the file system needs absolute paths for cache calculation
            string GetAbsolutePathFromRepositoryRoot(string relativePath)
            {
                return Path.Combine(repositoryRoot, relativePath);
            }
        }

        /// <summary>
        /// Should return a file path relative to the repository root
        /// </summary>
        /// <param name="repositoryRoot">The root of the repo</param>
        /// <param name="fullPath">The absolute path of an object elsewhere in this repository</param>
        public static string GetRelativePath(string repositoryRoot, string fullPath) =>
            Path.GetRelativePath(repositoryRoot, fullPath);
    }
}
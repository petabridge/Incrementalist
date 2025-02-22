// -----------------------------------------------------------------------
// <copyright file="DependencyCache.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.ProjectSystem;
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
        ImmutableDictionary<string, ProjectNode> Projects)
    {
        /// <summary>
        /// Determines which projects need to be rebuilt based on the affected files
        /// </summary>
        /// <param name="affectedFiles">Dictionary of affected files and their types</param>
        /// <returns>Dictionary where keys are affected project paths and values are collections of projects that depend on them (including the project itself)</returns>
        public Dictionary<string, ICollection<string>> GetProjectsToRebuild(Dictionary<string, SlnFile> affectedFiles)
        {
            var result = new Dictionary<string, ICollection<string>>();

            // Group affected files by their ProjectId or by their path for project files
            var affectedProjects = affectedFiles
                .Where(x => x.Value.FileType == FileType.Project || x.Value.ProjectId != null)
                .GroupBy(x => x.Value.FileType == FileType.Project ? x.Key : Projects.FirstOrDefault(p => p.Value.Id == x.Value.ProjectId).Key)
                .Where(g => g.Key != null)
                .Select(g => g.Key)
                .ToList();

            foreach (var affectedProject in affectedProjects)
            {
                var dependentProjects = new HashSet<string> { affectedProject };
                bool hasChanges;
                do
                {
                    hasChanges = false;
                    foreach (var project in Projects)
                    {
                        // If this project depends on any affected project and isn't already included
                        if (!dependentProjects.Contains(project.Key) &&
                            project.Value.Dependencies.Any(dep => dependentProjects.Contains(dep)))
                        {
                            dependentProjects.Add(project.Key);
                            hasChanges = true;
                        }
                    }
                } while (hasChanges);

                result[affectedProject] = dependentProjects;
            }

            return result;
        }
    }

    /// <summary>
    /// Represents a project and its direct dependencies in the solution
    /// </summary>
    public sealed record ProjectNode(
        string Path,
        ProjectId Id,
        ImmutableList<string> Dependencies);

    /// <summary>
    /// Handles serialization and deserialization of the dependency cache
    /// </summary>
    public static class DependencyCacheIO
    {
        public const string CurrentVersion = "1.0";
        
        public static string GetCachePath(string solutionDir) =>
            Path.Combine(solutionDir, IncrementalistFileConstants.IncrementalistDirectory, IncrementalistFileConstants.CacheFileName);
            
        public static async Task<DependencyCache?> LoadAsync(string path)
        {
            if (!File.Exists(path))
                return null;
                
            var json = await File.ReadAllTextAsync(path);
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ProjectIdJsonConverter());
            return JsonSerializer.Deserialize<DependencyCache>(json, options);
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
                
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new ProjectIdJsonConverter());
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(cache, options));
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
        /// <param name="solution">The current solution to validate against</param>
        /// <param name="logger">Optional logger for diagnostic information</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the cache is valid and can be used, false if it needs to be regenerated</returns>
        public static async Task<bool> IsCacheValidAsync(
            DependencyCache? cache,
            Solution solution,
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
            if (cache.Version != DependencyCacheIO.CurrentVersion)
            {
                logger?.LogInformation(
                    "Cache version mismatch. Expected {ExpectedVersion}, found {ActualVersion}",
                    DependencyCacheIO.CurrentVersion,
                    cache.Version);
                return false;
            }

            // Solution path mismatch
            if (!string.Equals(cache.SolutionPath, solution.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogInformation(
                    "Cache is for different solution. Expected {ExpectedPath}, found {ActualPath}",
                    solution.FilePath,
                    cache.SolutionPath);
                return false;
            }

            // Calculate current checksum
            var projectPaths = solution.Projects
                .Select(p => p.FilePath)
                .Where(p => p != null)
                .Cast<string>()
                .ToList();

            var currentChecksum = await ChecksumCalculator.CalculateChecksumAsync(
                solution.FilePath,
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
        /// <param name="solution">The solution to analyze</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A new DependencyCache instance</returns>
        public static async Task<DependencyCache> CreateFromSolutionAsync(
            Solution solution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(solution);

            // Get the dependency graph from Roslyn
            var dependencyGraph = solution.GetProjectDependencyGraph();

            // Build the Projects dictionary
            var projectsBuilder = ImmutableDictionary.CreateBuilder<string, ProjectNode>();
            foreach (var projectId in solution.ProjectIds)
            {
                var project = solution.GetProject(projectId);
                if (project?.FilePath == null) continue;

                var dependencies = dependencyGraph
                    .GetProjectsThatThisProjectDirectlyDependsOn(projectId)
                    .Select(depId => solution.GetProject(depId)?.FilePath)
                    .Where(path => path != null)
                    .Cast<string>()
                    .ToImmutableList();

                projectsBuilder.Add(project.FilePath, new ProjectNode(project.FilePath, projectId, dependencies));
            }

            // Calculate checksum for all project files
            var projectPaths = solution.Projects
                .Select(p => p.FilePath)
                .Where(p => p != null)
                .Cast<string>()
                .ToList();

            var checksum = await ChecksumCalculator.CalculateChecksumAsync(
                solution.FilePath,
                projectPaths,
                cancellationToken);

            return new DependencyCache(
                Version: DependencyCacheIO.CurrentVersion,
                SolutionPath: solution.FilePath!,
                Checksum: checksum,
                Projects: projectsBuilder.ToImmutable());
        }
    }
} 
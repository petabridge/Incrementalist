using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Incrementalist.ProjectSystem.Cache
{
    /// <summary>
    /// Represents a project node in the dependency tree
    /// </summary>
    public sealed class ProjectNode
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("dependencies")]
        public HashSet<string> Dependencies { get; set; } = new();
    }

    /// <summary>
    /// Represents a cached version of the solution's dependency graph using a tree structure
    /// </summary>
    public sealed class DependencyGraphCache
    {
        private const string CacheFileName = "dependency-graph.cache.json";
        private const string CacheFolderName = ".incrementalist";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("solutionPath")]
        public string SolutionPath { get; set; }

        [JsonPropertyName("projects")]
        public Dictionary<string, ProjectNode> Projects { get; set; } = new();

        [JsonPropertyName("checksum")]
        public string Checksum { get; set; }

        /// <summary>
        /// Creates a cache from Incrementalist's dependency graph format
        /// </summary>
        public static DependencyGraphCache FromDependencyGraph(string solutionPath, Dictionary<string, ICollection<string>> dependencyGraph)
        {
            var cache = new DependencyGraphCache
            {
                SolutionPath = solutionPath
            };

            // First pass: create all project nodes
            foreach (var project in dependencyGraph.Keys)
            {
                cache.Projects[project] = new ProjectNode { Path = project };
            }

            // Second pass: populate dependencies
            foreach (var (project, dependencies) in dependencyGraph)
            {
                // In Incrementalist's format, the dependencies are projects that depend on this project
                // So we need to invert the relationship for our cache format
                foreach (var dependent in dependencies)
                {
                    if (!cache.Projects.ContainsKey(dependent))
                    {
                        cache.Projects[dependent] = new ProjectNode { Path = dependent };
                    }
                    cache.Projects[dependent].Dependencies.Add(project);
                }
            }

            return cache;
        }

        /// <summary>
        /// Creates a cache from Roslyn's project dependency graph
        /// </summary>
        public static DependencyGraphCache FromRoslynDependencyGraph(Solution solution, ProjectDependencyGraph dependencyGraph)
        {
            var cache = new DependencyGraphCache
            {
                SolutionPath = solution.FilePath
            };

            foreach (var project in solution.Projects)
            {
                var dependencies = dependencyGraph.GetProjectsThatThisProjectDirectlyDependsOn(project.Id)
                    .Select(id => solution.GetProject(id).FilePath)
                    .ToHashSet();

                cache.Projects[project.FilePath] = new ProjectNode
                {
                    Path = project.FilePath,
                    Dependencies = dependencies
                };
            }

            return cache;
        }

        /// <summary>
        /// Converts the cache to Incrementalist's dependency graph format
        /// </summary>
        public Dictionary<string, ICollection<string>> ToIncrementalistDependencyGraph()
        {
            var result = new Dictionary<string, ICollection<string>>();

            foreach (var project in Projects.Keys)
            {
                // Get all projects that depend on this one (including transitive dependencies)
                var dependentProjects = GetDependentProjects(project);
                if (dependentProjects.Count > 0)  // Only include projects that have dependents
                {
                    result[project] = dependentProjects;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all projects that depend on the specified project
        /// </summary>
        public HashSet<string> GetDependentProjects(string projectPath)
        {
            var result = new HashSet<string>();
            foreach (var project in Projects.Values)
            {
                if (project.Dependencies.Contains(projectPath))
                {
                    result.Add(project.Path);
                    // Also add any projects that depend on this dependent project
                    result.UnionWith(GetDependentProjects(project.Path));
                }
            }
            return result;
        }

        /// <summary>
        /// Gets all projects that the specified project depends on
        /// </summary>
        public HashSet<string> GetProjectDependencies(string projectPath)
        {
            if (!Projects.TryGetValue(projectPath, out var project))
                return new HashSet<string>();

            var result = new HashSet<string>(project.Dependencies);
            foreach (var dependency in project.Dependencies.ToList())
            {
                // Add transitive dependencies
                result.UnionWith(GetProjectDependencies(dependency));
            }
            return result;
        }

        /// <summary>
        /// Calculates a checksum for all project and solution files to detect changes
        /// </summary>
        public static string CalculateChecksum(string solutionPath, IEnumerable<string> projectFiles)
        {
            using var sha256 = SHA256.Create();
            var allFiles = new List<string> { solutionPath };
            allFiles.AddRange(projectFiles);

            var checksums = new List<byte>();
            foreach (var file in allFiles.OrderBy(f => f)) // Sort for consistency
            {
                if (!File.Exists(file)) continue;
                var fileBytes = File.ReadAllBytes(file);
                var fileHash = sha256.ComputeHash(fileBytes);
                checksums.AddRange(fileHash);
            }

            var finalHash = sha256.ComputeHash(checksums.ToArray());
            return Convert.ToBase64String(finalHash);
        }

        /// <summary>
        /// Gets the path to the cache file for a given solution
        /// </summary>
        public static string GetCacheFilePath(string solutionPath)
        {
            var solutionDir = Path.GetDirectoryName(solutionPath);
            var cacheDir = Path.Combine(solutionDir, CacheFolderName);
            return Path.Combine(cacheDir, CacheFileName);
        }

        /// <summary>
        /// Loads the cached dependency graph if it exists and is valid
        /// </summary>
        public static DependencyGraphCache Load(string solutionPath, IEnumerable<string> projectFiles)
        {
            var cacheFile = GetCacheFilePath(solutionPath);
            if (!File.Exists(cacheFile))
                return null;

            try
            {
                var json = File.ReadAllText(cacheFile);
                var cache = JsonSerializer.Deserialize<DependencyGraphCache>(json);

                // Verify the solution path matches
                if (cache.SolutionPath != solutionPath)
                    return null;

                // Calculate current checksum and compare
                var currentChecksum = CalculateChecksum(solutionPath, projectFiles);
                if (cache.Checksum != currentChecksum)
                    return null;

                return cache;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves the dependency graph cache to disk
        /// </summary>
        public void Save()
        {
            var cacheFile = GetCacheFilePath(SolutionPath);
            var cacheDir = Path.GetDirectoryName(cacheFile);

            // Ensure cache directory exists
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            File.WriteAllText(cacheFile, json);
        }
    }
} 
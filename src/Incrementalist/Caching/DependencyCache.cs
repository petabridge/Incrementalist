// -----------------------------------------------------------------------
// <copyright file="DependencyCache.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Incrementalist.Caching
{
    /// <summary>
    /// Represents the cached dependency information for a solution
    /// </summary>
    public sealed record DependencyCache(
        string Version,
        string SolutionPath,
        string Checksum,
        ImmutableDictionary<string, ProjectNode> Projects);

    /// <summary>
    /// Represents a project and its direct dependencies in the solution
    /// </summary>
    public sealed record ProjectNode(
        string Path,
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
            return JsonSerializer.Deserialize<DependencyCache>(json);
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
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(cache, options));
        }
    }
} 
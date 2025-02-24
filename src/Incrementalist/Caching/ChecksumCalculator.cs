// -----------------------------------------------------------------------
// <copyright file="ChecksumCalculator.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Incrementalist.Caching
{
    /// <summary>
    /// Calculates checksums for solution and project files to detect changes
    /// </summary>
    public static class ChecksumCalculator
    {
        /// <summary>
        /// Calculates a checksum for a solution and its project files
        /// </summary>
        /// <param name="solutionPath">Path to the solution file</param>
        /// <param name="projectPaths">Paths to all project files in the solution</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A base64-encoded checksum string</returns>
        public static async Task<string> CalculateChecksumAsync(
            string solutionPath,
            IEnumerable<string> projectPaths,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(solutionPath);
            ArgumentNullException.ThrowIfNull(projectPaths);

            using var sha256 = SHA256.Create();
            using var ms = new MemoryStream();

            // Add solution file content and timestamp
            await AddFileToChecksumAsync(ms, solutionPath, cancellationToken);

            // Add each project file content and timestamp, sorted for consistency
            foreach (var projectPath in projectPaths.OrderBy(x => x))
            {
                await AddFileToChecksumAsync(ms, projectPath, cancellationToken);
            }

            // Calculate final hash
            ms.Position = 0;
            var hash = await sha256.ComputeHashAsync(ms, cancellationToken);
            return Convert.ToBase64String(hash);
        }

        private static async Task AddFileToChecksumAsync(
            Stream stream,
            string filePath,
            CancellationToken cancellationToken)
        {
            // Add file content
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            await stream.WriteAsync(fileBytes, cancellationToken);

            // Add file timestamp
            var fileInfo = new FileInfo(filePath);
            var timeBytes = BitConverter.GetBytes(fileInfo.LastWriteTimeUtc.Ticks);
            await stream.WriteAsync(timeBytes, cancellationToken);
        }
    }
} 
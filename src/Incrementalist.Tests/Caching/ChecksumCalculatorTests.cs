// -----------------------------------------------------------------------
// <copyright file="ChecksumCalculatorTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using Incrementalist.Caching;
using Incrementalist.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Caching
{
    public sealed class ChecksumCalculatorTests : IDisposable
    {
        private readonly DisposableRepository _repository;
        private readonly ITestOutputHelper _output;

        public ChecksumCalculatorTests(ITestOutputHelper output)
        {
            _repository = new DisposableRepository();
            _output = output;
        }

        public void Dispose()
        {
            _repository.Dispose();
        }

        [Fact]
        public async Task CalculateChecksum_WithSameFiles_ReturnsSameChecksum()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            await File.WriteAllTextAsync(solutionPath, "dummy solution content");
            await File.WriteAllTextAsync(project1Path, "<Project />");
            await File.WriteAllTextAsync(project2Path, "<Project />");

            // Act
            var checksum1 = await ChecksumCalculator.CalculateChecksumAsync(
                solutionPath, new[] { project1Path, project2Path });
            var checksum2 = await ChecksumCalculator.CalculateChecksumAsync(
                solutionPath, new[] { project1Path, project2Path });

            // Assert
            Assert.Equal(checksum1, checksum2);
        }

        [Fact]
        public async Task CalculateChecksum_WithModifiedFile_ReturnsDifferentChecksum()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            await File.WriteAllTextAsync(solutionPath, "dummy solution content");
            await File.WriteAllTextAsync(project1Path, "<Project />");
            await File.WriteAllTextAsync(project2Path, "<Project />");

            var checksum1 = await ChecksumCalculator.CalculateChecksumAsync(
                solutionPath, new[] { project1Path, project2Path });

            // Modify a file
            await File.WriteAllTextAsync(project1Path, "<Project><Modified /></Project>");

            // Act
            var checksum2 = await ChecksumCalculator.CalculateChecksumAsync(
                solutionPath, new[] { project1Path, project2Path });

            // Assert
            Assert.NotEqual(checksum1, checksum2);
        }

        [Fact]
        public async Task CalculateChecksum_WithDifferentOrder_ReturnsSameChecksum()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            await File.WriteAllTextAsync(solutionPath, "dummy solution content");
            await File.WriteAllTextAsync(project1Path, "<Project />");
            await File.WriteAllTextAsync(project2Path, "<Project />");

            // Act
            var checksum1 = await ChecksumCalculator.CalculateChecksumAsync(
                solutionPath, new[] { project1Path, project2Path });
            var checksum2 = await ChecksumCalculator.CalculateChecksumAsync(
                solutionPath, new[] { project2Path, project1Path });

            // Assert
            Assert.Equal(checksum1, checksum2);
        }

        [Fact]
        public async Task CalculateChecksum_WithMissingFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            var project1Path = Path.Combine(_repository.BasePath, "src", "Project1", "Project1.csproj");
            var project2Path = Path.Combine(_repository.BasePath, "src", "Project2", "Project2.csproj");

            // Create directories for both projects
            Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);

            // Only create solution and one project file
            await File.WriteAllTextAsync(solutionPath, "dummy solution content");
            await File.WriteAllTextAsync(project1Path, "<Project />");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                ChecksumCalculator.CalculateChecksumAsync(
                    solutionPath,
                    new[] { project1Path, project2Path }));
        }

        [Theory]
        [InlineData(null)]
        public async Task CalculateChecksum_WithNullSolutionPath_ThrowsArgumentNullException(string solutionPath)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                ChecksumCalculator.CalculateChecksumAsync(
                    solutionPath,
                    Array.Empty<string>()));
        }

        [Fact]
        public async Task CalculateChecksum_WithNullProjectPaths_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                ChecksumCalculator.CalculateChecksumAsync(
                    "test.sln",
                    null!));
        }
    }
} 
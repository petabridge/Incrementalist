// -----------------------------------------------------------------------
// <copyright file="GlobFilterTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Incrementalist.Cmd;
using Xunit;

namespace Incrementalist.Tests;

public class GlobFilterTests
{
    public class AbsolutePaths
    {
        public class LinuxPaths
        {
            public static readonly AbsolutePath BasePath = new AbsolutePath("/usr/a/repositories/Akka.Management");

            public static readonly IReadOnlyList<AbsolutePath> LinuxTestProjects = new List<string>
            {
                "/usr/a/repositories/Akka.Management/src/ProjectA/ProjectA.csproj",
                "/usr/a/repositories/Akka.Management/src/ProjectB/ProjectB.csproj",
                "/usr/a/repositories/Akka.Management/tests/ProjectA.Tests/ProjectA.Tests.csproj",
                "/usr/a/repositories/Akka.Management/tests/ProjectB.Tests/ProjectB.Tests.csproj",
                "/usr/a/repositories/Akka.Management/samples/Sample1/Sample1.csproj",
                "/usr/a/repositories/Akka.Management/samples/Sample2/Sample2.fsproj" // Different extension
            }.Select(c => new AbsolutePath(c)).ToList().AsReadOnly();

            [Theory]
            [InlineData(new[] { "src/**/*.csproj" }, 2)] // Match src projects
            [InlineData(new[] { "src/**/*" }, 2)] // Match src projects
            [InlineData(new[] { "**/ProjectA*" }, 2)] // Match ProjectA and ProjectA.Tests
            [InlineData(new[] { "**/*.csproj" }, 5)] // Match all csproj
            [InlineData(new[] { "**/*.fsproj" }, 1)] // Match the fsproj
            [InlineData(new[] { "nonexistent/**" }, 0)] // Match none
            [InlineData(new[] { "**/src/ProjectA/ProjectA.csproj" }, 1)] // Exact match
            [InlineData(new[] { "**/ProjectA*", "**/ProjectB*" }, 4)] // Multiple patterns
            [InlineData(new[] { "SRC/**/*.csproj" }, 2)] // Case-insensitive check
            public void FilterProjects_OnlyTargetFilters_ReturnsMatchingProjects(string[] targetGlobs,
                int expectedCount)
            {
                // Arrange
                var skipGlobs = Array.Empty<string>();

                // Act
                var relativePaths = LinuxTestProjects.Select(p => BasePath.ComputeRelativePathToMe(p)).ToList();
                var result = GlobFilter.FilterProjects(relativePaths, skipGlobs, targetGlobs);

                // Assert
                Assert.Equal(expectedCount, result.Count);
            }
        }

        public class WindowsPaths
        {
            public static readonly AbsolutePath BasePath = new AbsolutePath(@"C:\repositories\Akka.Management");

            public static readonly IReadOnlyList<AbsolutePath> WindowsTestProjects = new List<string>
            {
                @"C:\repositories\Akka.Management\src\ProjectA\ProjectA.csproj",
                @"C:\repositories\Akka.Management\src\ProjectB\ProjectB.csproj",
                @"C:\repositories\Akka.Management\tests\ProjectA.Tests\ProjectA.Tests.csproj",
                @"C:\repositories\Akka.Management\tests\ProjectB.Tests\ProjectB.Tests.csproj",
                @"C:\repositories\Akka.Management\samples\Sample1\Sample1.csproj",
                @"C:\repositories\Akka.Management\samples\Sample2\Sample2.fsproj" // Different extension
            }.Select(c => new AbsolutePath(c)).ToList().AsReadOnly();

            [Theory]
            [InlineData(new[] { "src/**/*.csproj" }, 2)] // Match src projects
            [InlineData(new[] { "src/**/*" }, 2)] // Match src projects
            [InlineData(new[] { "**/ProjectA*" }, 2)] // Match ProjectA and ProjectA.Tests
            [InlineData(new[] { "**/*.csproj" }, 5)] // Match all csproj
            [InlineData(new[] { "**/*.fsproj" }, 1)] // Match the fsproj
            [InlineData(new[] { "nonexistent/**" }, 0)] // Match none
            [InlineData(new[] { "src/ProjectA/ProjectA.csproj" }, 1)] // Exact match
            [InlineData(new[] { "**/ProjectA*", "**/ProjectB*" }, 4)] // Multiple patterns
            [InlineData(new[] { "SRC/**" }, 2)] // Case-insensitive check
            public void FilterProjects_OnlyTargetFilters_ReturnsMatchingProjects(string[] targetGlobs,
                int expectedCount)
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // skip
                    return;
                }

                // Arrange
                var skipGlobs = Array.Empty<string>();

                // Act
                var relativePaths = WindowsTestProjects.Select(p => BasePath.ComputeRelativePathToMe(p)).ToList();
                var result = GlobFilter.FilterProjects(relativePaths, skipGlobs, targetGlobs);

                // Assert
                Assert.Equal(expectedCount, result.Count);
            }
        }
    }

    public class RelativePaths
    {
        public static readonly IReadOnlyList<RelativePath> TestProjects = new List<string>
        {
            "src/ProjectA/ProjectA.csproj",
            "src/ProjectB/ProjectB.csproj",
            "tests/ProjectA.Tests/ProjectA.Tests.csproj",
            "tests/ProjectB.Tests/ProjectB.Tests.csproj",
            "samples/Sample1/Sample1.csproj",
            "samples/Sample2/Sample2.fsproj" // Different extension
        }.Select(c => new RelativePath(c)).ToList().AsReadOnly();

        [Fact]
        public void FilterProjects_NoFilters_ReturnsOriginalList()
        {
            // Arrange
            var skipGlobs = Array.Empty<string>();
            var targetGlobs = Array.Empty<string>();

            // Act
            var result = GlobFilter.FilterProjects(TestProjects, skipGlobs, targetGlobs);

            // Assert
            Assert.Equivalent(TestProjects, result);
        }

        [Fact]
        public void FilterProjects_EmptyInput_ReturnsEmptyList()
        {
            // Arrange
            var emptyProjects = Array.Empty<RelativePath>();
            var skipGlobs = new[] { "**/ProjectA*" };
            var targetGlobs = new[] { "src/**" };

            // Act
            var result = GlobFilter.FilterProjects(emptyProjects, skipGlobs, targetGlobs);

            // Assert
            Assert.Empty(result ?? []);
        }

        [Theory]
        [InlineData(new[] { "src/**/*.csproj" }, 2)] // Match src projects
        [InlineData(new[] { "src/**/*" }, 2)] // Match src projects
        [InlineData(new[] { "**/ProjectA*" }, 2)] // Match ProjectA and ProjectA.Tests
        [InlineData(new[] { "**/*.csproj" }, 5)] // Match all csproj
        [InlineData(new[] { "**/*.fsproj" }, 1)] // Match the fsproj
        [InlineData(new[] { "nonexistent/**" }, 0)] // Match none
        [InlineData(new[] { "src/ProjectA/ProjectA.csproj" }, 1)] // Exact match
        [InlineData(new[] { "**/ProjectA*", "**/ProjectB*" }, 4)] // Multiple patterns
        [InlineData(new[] { "SRC/**/*.csproj" }, 2)] // Case-insensitive check
        public void FilterProjects_OnlyTargetFilters_ReturnsMatchingProjects(string[] targetGlobs, int expectedCount)
        {
            // Arrange
            var skipGlobs = Array.Empty<string>();

            // Act
            var result = GlobFilter.FilterProjects(TestProjects, skipGlobs, targetGlobs);

            // Assert
            Assert.Equal(expectedCount, result.Count);
        }

        [Theory]
        [InlineData(new[] { "tests/**" }, 4)] // Skip tests, keep src and samples (6 - 2 = 4)
        [InlineData(new[] { "**/ProjectA*" }, 4)] // Skip ProjectA and ProjectA.Tests (6 - 2 = 4)
        [InlineData(new[] { "**/*.fsproj" }, 5)] // Skip the fsproj (6 - 1 = 5)
        [InlineData(new[] { "nonexistent/**" }, 6)] // Skip none
        [InlineData(new[] { "src/ProjectB/ProjectB.csproj" }, 5)] // Skip exact match
        [InlineData(new[] { "tests/**", "samples/**" }, 2)] // Skip tests and samples (6 - 2 - 2 = 2)
        [InlineData(new[] { "TESTS/**" }, 4)] // Case-insensitive check
        public void FilterProjects_OnlySkipFilters_ReturnsNonMatchingProjects(string[] skipGlobs, int expectedCount)
        {
            // Arrange
            var targetGlobs = Array.Empty<string>();

            // Act
            var result = GlobFilter.FilterProjects(TestProjects, skipGlobs, targetGlobs);

            // Assert
            Assert.Equal(expectedCount, result.Count);
        }

        [Theory]
        // Target 'src/**', then skip '*B*' -> Should only include ProjectA.csproj
        [InlineData(new[] { "src/**" }, new[] { "**/ProjectB*" }, new[] { "src/ProjectA/ProjectA.csproj" })]
        [InlineData(new[] { "src/**/*.csproj" }, new[] { "**/ProjectB*" }, new[] { "src/ProjectA/ProjectA.csproj" })]
        // Target '**/*Tests*', then skip '*B*' -> Should only include ProjectA.Tests.csproj
        [InlineData(new[] { "**/*Tests*" }, new[] { "**/ProjectB*" },
            new[] { "tests/ProjectA.Tests/ProjectA.Tests.csproj" })]
        // Target 'src/**', skip 'nonexistent/**' -> Should include both src projects
        [InlineData(new[] { "src/**" }, new[] { "nonexistent/**" },
            new[] { "src/ProjectA/ProjectA.csproj", "src/ProjectB/ProjectB.csproj" })]
        // Target 'nonexistent/**', skip 'tests/**' -> Should be empty
        [InlineData(new[] { "nonexistent/**" }, new[] { "tests/**" }, new string[] { })]
        // Target all (empty targetGlobs), skip tests -> Should include src and samples
        [InlineData(new string[] { }, new[] { "tests/**" },
            new[]
            {
                "src/ProjectA/ProjectA.csproj", "src/ProjectB/ProjectB.csproj", "samples/Sample1/Sample1.csproj",
                "samples/Sample2/Sample2.fsproj"
            })]
        // Target tests, skip all -> Should be empty
        [InlineData(new[] { "tests/**" }, new[] { "**/*" }, new string[] { })]
        [InlineData(new[] { "**/*ProjectA*", "samples/**/*.*sproj" }, new[] { "tests/**" }, new
            []
            {
                // should filter out the ProjectA tests, but keep ProjectA itself and the samples
                "src/ProjectA/ProjectA.csproj",
                "samples/Sample1/Sample1.csproj",
                "samples/Sample2/Sample2.fsproj"
            })]
        public void FilterProjects_BothFilters_AppliesTargetThenSkip(string[] targetGlobs, string[] skipGlobs,
            string[] expectedProjects)
        {
            // Arrange

            // Act
            var result = GlobFilter.FilterProjects(TestProjects, skipGlobs, targetGlobs);

            // Assert
            var expectedRelativePaths = expectedProjects.Select(p => new RelativePath(p)).ToList();
            Assert.Equivalent(expectedRelativePaths, result);
        }
    }
}
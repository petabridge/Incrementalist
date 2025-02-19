// -----------------------------------------------------------------------
// <copyright file="SolutionWideChangeDetectorTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Xunit;

namespace Incrementalist.Tests
{
    public class SolutionWideChangeDetectorTests
    {
        private readonly SolutionWideChangeDetector _detector;

        public SolutionWideChangeDetectorTests()
        {
            _detector = new SolutionWideChangeDetector();
        }

        [Theory]
        [InlineData("Directory.Build.props")]
        [InlineData("Directory.Packages.props")]
        [InlineData("global.json")]
        [InlineData("nuget.config")]
        [InlineData("some/path/Directory.Build.props")]
        public void DirectMatch_SolutionWideFiles_ReturnsTrue(string fileName)
        {
            var changes = new[] { fileName };
            Assert.True(_detector.RequiresFullSolutionBuild(changes));
        }

        [Theory]
        [InlineData("MySolution.sln")]
        [InlineData("Common.props")]
        [InlineData("Build.targets")]
        [InlineData("some/path/MySolution.sln")]
        public void ExtensionMatch_SolutionWideFiles_ReturnsTrue(string fileName)
        {
            var changes = new[] { fileName };
            Assert.True(_detector.RequiresFullSolutionBuild(changes));
        }

        [Theory]
        [InlineData("src/Project1/Project1.csproj")]
        [InlineData("src/Project1/Class1.cs")]
        [InlineData("README.md")]
        public void NonSolutionWideFiles_ReturnsFalse(string fileName)
        {
            var changes = new[] { fileName };
            Assert.False(_detector.RequiresFullSolutionBuild(changes));
        }

        [Fact]
        public void MultipleChanges_WithOneSolutionWideFile_ReturnsTrue()
        {
            var changes = new[]
            {
                "src/Project1/Class1.cs",
                "Directory.Build.props",
                "src/Project2/Class2.cs"
            };
            Assert.True(_detector.RequiresFullSolutionBuild(changes));
        }

        [Fact]
        public void NullChanges_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _detector.RequiresFullSolutionBuild(null));
        }

        [Theory]
        [InlineData("DIRECTORY.BUILD.PROPS")]
        [InlineData("directory.build.props")]
        [InlineData("DiReCtOrY.bUiLd.PrOpS")]
        public void FileNameMatching_IsCaseInsensitive(string fileName)
        {
            var changes = new[] { fileName };
            Assert.True(_detector.RequiresFullSolutionBuild(changes));
        }

        [Theory]
        [InlineData("test.SLN")]
        [InlineData("test.PROPS")]
        [InlineData("test.TARGETS")]
        public void ExtensionMatching_IsCaseInsensitive(string fileName)
        {
            var changes = new[] { fileName };
            Assert.True(_detector.RequiresFullSolutionBuild(changes));
        }
    }
} 
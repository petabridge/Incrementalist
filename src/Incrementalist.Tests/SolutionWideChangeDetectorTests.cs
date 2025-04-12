// -----------------------------------------------------------------------
// <copyright file="SolutionWideChangeDetectorTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Incrementalist.ProjectSystem;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Incrementalist.Tests
{
    public class SolutionWideChangeDetectorTests : IDisposable
    {
        private readonly DisposableRepository _repository;
        private readonly SolutionWideChangeDetector _detector;
        private readonly Solution _solution;

        public SolutionWideChangeDetectorTests()
        {
            _repository = new DisposableRepository();
            
            // Create a sample solution with project imports
            var sample = ProjectSampleGenerator.GetProjectWithImportSample("TestProject.csproj");
            
            // Write files to the repository
            _repository.WriteFile(sample.ProjectFile);
            _repository.WriteFile(sample.ImportedPropsFile);
            
            // Create a solution file
            var solutionPath = Path.Combine(_repository.BasePath, "test.sln");
            File.WriteAllText(solutionPath, ""); // Empty solution file is sufficient for our tests
            
            // Create some solution-wide files
            File.WriteAllText(Path.Combine(_repository.BasePath, "Directory.Build.props"), "<Project />");
            File.WriteAllText(Path.Combine(_repository.BasePath, "Directory.Packages.props"), "<Project />");
            
            // Load the solution
            var workspace = new AdhocWorkspace();
            var solutionInfo = SolutionInfo.Create(
                SolutionId.CreateNewId(),
                VersionStamp.Create(),
                solutionPath);
            
            _solution = workspace.AddSolution(solutionInfo);
            _detector = new SolutionWideChangeDetector(_solution);
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
        [InlineData("some/path/MySolution.sln")]
        public void SolutionFile_ReturnsTrue(string fileName)
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
            Assert.Throws<ArgumentNullException>(() => _detector.RequiresFullSolutionBuild(null!));
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
        public void ExtensionMatching_IsCaseInsensitive(string fileName)
        {
            var changes = new[] { fileName };
            Assert.True(_detector.RequiresFullSolutionBuild(changes));
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
} 
// -----------------------------------------------------------------------
// <copyright file="SolutionFinderSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2023 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Incrementalist.ProjectSystem;
using Incrementalist.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.ProjectSystem
{
    public class SolutionFinderSpecs : IDisposable
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DisposableRepository _repository;

        public SolutionFinderSpecs(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _repository = new DisposableRepository();
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }

        [Fact(DisplayName = "Should find single solution in root directory")]
        public void Should_Find_Single_Solution()
        {
            // Arrange
            var solutionContent = "dummy solution content";
            _repository.WriteFile("MySolution.sln", solutionContent);

            // Act
            var solutions = SolutionFinder.GetSolutions(_repository.BasePath).ToList();

            // Assert
            solutions.Should().HaveCount(1);
            solutions.First().Should().EndWith("MySolution.sln");
        }

        [Fact(DisplayName = "Should find multiple solutions in root directory")]
        public void Should_Find_Multiple_Solutions()
        {
            // Arrange
            var solutionContent = "dummy solution content";
            _repository.WriteFile("Solution1.sln", solutionContent)
                      .WriteFile("Solution2.sln", solutionContent);

            // Act
            var solutions = SolutionFinder.GetSolutions(_repository.BasePath).ToList();

            // Assert
            solutions.Should().HaveCount(2);
            solutions.Should().Contain(x => x.EndsWith("Solution1.sln"));
            solutions.Should().Contain(x => x.EndsWith("Solution2.sln"));
        }

        [Fact(DisplayName = "Should find solutions in subdirectories")]
        public void Should_Find_Solutions_In_Subdirectories()
        {
            // Arrange
            var solutionContent = "dummy solution content";
            var expectedPath = Path.Join("src", "MySolution.sln");
            Directory.CreateDirectory(Path.Combine(_repository.BasePath, "src"));
            _repository.WriteFile("src/MySolution.sln", solutionContent);

            // Act
            var solutions = SolutionFinder.GetSolutions(_repository.BasePath).ToList();

            // Assert
            solutions.Should().HaveCount(1);
            solutions.First().Should().EndWith(expectedPath);
        }

        [Fact(DisplayName = "Should return empty list when no solutions found")]
        public void Should_Return_Empty_When_No_Solutions()
        {
            // Act
            var solutions = SolutionFinder.GetSolutions(_repository.BasePath).ToList();

            // Assert
            solutions.Should().BeEmpty();
        }

        [Fact(DisplayName = "Should respect search filter when provided")]
        public void Should_Respect_Search_Filter()
        {
            // Arrange
            var solutionContent = "dummy solution content";
            _repository.WriteFile("Solution1.sln", solutionContent)
                      .WriteFile("Test.sln", solutionContent);

            // Act
            var solutions = SolutionFinder.GetSolutions(_repository.BasePath, "Test*.sln").ToList();

            // Assert
            solutions.Should().HaveCount(1);
            solutions.First().Should().EndWith("Test.sln");
        }

        [Fact(DisplayName = "Should respect search option when provided")]
        public void Should_Respect_Search_Option()
        {
            // Arrange
            var solutionContent = "dummy solution content";
            Directory.CreateDirectory(Path.Combine(_repository.BasePath, "src"));
            _repository.WriteFile("Solution1.sln", solutionContent)
                      .WriteFile("src/Solution2.sln", solutionContent);

            // Act - TopDirectoryOnly
            var topDirSolutions = SolutionFinder.GetSolutions(_repository.BasePath, searchOption: SearchOption.TopDirectoryOnly).ToList();

            // Assert
            topDirSolutions.Should().HaveCount(1);
            topDirSolutions.First().Should().EndWith("Solution1.sln");
        }

        [Fact(DisplayName = "Should process multiple solutions in deterministic order")]
        public void Should_Process_Multiple_Solutions_In_Order()
        {
            // Arrange
            var solutionContent = "dummy solution content";
            _repository.WriteFile("A.Solution.sln", solutionContent)
                      .WriteFile("B.Solution.sln", solutionContent)
                      .WriteFile("C.Solution.sln", solutionContent);

            // Act
            var solutions = SolutionFinder.GetSolutions(_repository.BasePath).ToList();

            // Assert
            solutions.Should().HaveCount(3);
            // Verify solutions are returned in alphabetical order
            solutions.Select(s => Path.GetFileName(s)).Should().BeInAscendingOrder();
        }
    }
} 
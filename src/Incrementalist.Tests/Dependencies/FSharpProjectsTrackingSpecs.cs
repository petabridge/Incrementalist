using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Incrementalist.ProjectSystem;
using Incrementalist.ProjectSystem.Cmds;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Dependencies
{
    public class FSharpProjectsTrackingSpecs : IDisposable
    {
        private readonly ITestOutputHelper _outputHelper;
        public DisposableRepository Repository { get; }
        
        public FSharpProjectsTrackingSpecs(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            Repository = new DisposableRepository();
        }

        public void Dispose()
        {
            Repository?.Dispose();
        }
        
        [Fact]
        public async Task FSharpProjectDiff_should_be_tracked()
        {
            var sample = ProjectSampleGenerator.GetFSharpSolutionSample("FSharpSolution.sln");
            var solutionFullPath = sample.SolutionFile.GetFullPath(Repository.BasePath);
            var fsharpProjectFullPath = sample.FSharpProjectFile.GetFullPath(Repository.BasePath);
            var csharpProjectFullPath = sample.CSharpProjectFile.GetFullPath(Repository.BasePath);

            Repository
                .WriteFile(sample.SolutionFile)
                .WriteFile(sample.CSharpProjectFile)
                .WriteFile(sample.FSharpProjectFile)
                .Commit("Created new solution with fsharp and csharp projects")
                .CreateBranch("foo")
                .CheckoutBranch("foo")
                .WriteFile(sample.CSharpProjectFile.Name, sample.CSharpProjectFile.Content + " ")
                .WriteFile(sample.FSharpProjectFile.Name, sample.FSharpProjectFile.Content + " ")
                .Commit("Updated both project files");
            
            var cmd = new FilterAffectedProjectFilesCmd(new TestOutputLogger(_outputHelper), new CancellationToken(), Repository.BasePath, "master");
            var solutionFiles = new Dictionary<string, SlnFile>()
            {
                [solutionFullPath] = new SlnFile(FileType.Solution, ProjectId.CreateNewId()),
                [fsharpProjectFullPath] = new SlnFile(FileType.Project, ProjectId.CreateNewId()),
                [csharpProjectFullPath] = new SlnFile(FileType.Project, ProjectId.CreateNewId())
            };
            var filteredAffectedFiles = await cmd.Process(Task.FromResult(solutionFiles));
            filteredAffectedFiles.Should().HaveCount(2).And.Subject.Keys.Should().BeEquivalentTo(fsharpProjectFullPath, csharpProjectFullPath);
        }
    }
}
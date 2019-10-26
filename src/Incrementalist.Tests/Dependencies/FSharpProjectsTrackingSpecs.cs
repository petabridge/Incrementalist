using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Incrementalist.Cmd.Commands;
using Incrementalist.ProjectSystem;
using Incrementalist.ProjectSystem.Cmds;
using Incrementalist.Tests.Helpers;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
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
            
            var logger = new TestOutputLogger(_outputHelper);
            var settings = new BuildSettings("master", solutionFullPath, Repository.BasePath);
            var workspace = SetupMsBuildWorkspace();
            var emitTask = new EmitDependencyGraphTask(settings, workspace, logger);
            var affectedFiles = (await emitTask.Run()).ToList();

            affectedFiles.Select(f => f.Key).Should().HaveCount(2).And.Subject.Should().BeEquivalentTo(fsharpProjectFullPath, csharpProjectFullPath);
        }

        private static MSBuildWorkspace SetupMsBuildWorkspace()
        {
            // Locate and register the default instance of MSBuild installed on this machine.
            MSBuildLocator.RegisterDefaults();
            
            return MSBuildWorkspace.Create();
        }
    }
}
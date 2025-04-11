using System;
using System.Threading.Tasks;
using Incrementalist.Cmd.Commands;
using Incrementalist.ProjectSystem;
using Incrementalist.ProjectSystem.Cmds;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis.MSBuild;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Dependencies
{
    [Collection(MSBuildCollectionFixture.Name)]
    public class FSharpProjectsTrackingSpecs : IDisposable
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly MSBuildWorkspace _workspace;
        public DisposableRepository Repository { get; }
        
        public FSharpProjectsTrackingSpecs(ITestOutputHelper outputHelper, MSBuildFixture fixture)
        {
            _outputHelper = outputHelper;
            _workspace = fixture.Workspace;
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
            var emitTask = new EmitDependencyGraphTask(settings, logger);
            var buildResult = await emitTask.Run();

            // When all projects are affected, we expect a full solution build
            Assert.True((buildResult) is FullSolutionBuildResult);
            var fullBuildResult = (FullSolutionBuildResult)buildResult;
            Assert.Equal(solutionFullPath, fullBuildResult.SolutionPath);
        }
    }
}
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Cmd.Commands;
using Incrementalist.Git;
using Incrementalist.ProjectSystem.Cmds;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Dependencies;

[Collection(MSBuildCollectionFixture.Name)]
public class EmitDependencyGraphSpecs : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly MSBuildWorkspace _workspace;
    private readonly TestSolutionModel _generatedTestSolution;
    private readonly ILogger _logger;
    
    private const string PrimaryBranch = "dev";
    private const string SecondaryBranch = "fixes";
    
    public DisposableRepository Repository { get; }
    
    public EmitDependencyGraphSpecs(ITestOutputHelper outputHelper, MSBuildFixture fixture)
    {
        _outputHelper = outputHelper;
        _workspace = fixture.Workspace;
        Repository = new DisposableRepository();
        _generatedTestSolution = CreateSolution();
        _logger = new TestOutputLogger(outputHelper);
    }

    private BuildSettings GetBuildSettings() =>
        new BuildSettings(PrimaryBranch, _generatedTestSolution.FileName, Repository.BasePath);

    private static TestSolutionModel CreateSolution()
    {
        var solutionBuilder = new TestSolutionBuilder("SampleSolution")
            .AddFolder("src", f1Builder =>
            {
                f1Builder.AddProject("ProjectA", (_, p1Builder) =>
                {
                    p1Builder.WithFile("HelloWorld.cs", CsharpSamples.HelloClass);
                });
                
                f1Builder.AddProject("ProjectB", (otherProjects, p2Builder) =>
                {
                    p2Builder.WithFile("GoodBye.cs", CsharpSamples.GoodbyeClassWithNamespace);
                    var projectA = otherProjects.First(p => p.NameWithoutExtension == "ProjectA");
                    p2Builder.WithProjectReference(projectA);
                });
            })
            .AddFolder("test", f2Builder =>
            {
                f2Builder.AddProject("ProjectB.Tests", (otherProjects, p3Builder) =>
                {
                    p3Builder.WithFile("HelloWorldTests.cs", CsharpSamples.BarClass);
                    var projectB = otherProjects.First(p => p.NameWithoutExtension == "ProjectB");
                    p3Builder.WithProjectReference(projectB);
                });
            })
            .Build();
        
        return solutionBuilder;
    }

    [Fact]
    public async Task ShouldDetectProjectCChanges()
    {
        // arrange
        var newFile = new SampleFile("NewFile.cs", CsharpSamples.FooClass);
        var projectC = _generatedTestSolution.FlatProjects.Single(p => p.NameWithoutExtension == "ProjectB.Tests");
        Repository
            .AddOrModifyProjectFile(projectC, newFile)
            .Commit("Added new file"); // should create the diffs
        
        // validate that we can detect the changes
        var diffs = DiffHelper.ChangedFiles(Repository.Repository, PrimaryBranch).ToList();
        Assert.NotEmpty(diffs);
        
        var cmd = new EmitDependencyGraphTask(GetBuildSettings(), _workspace, _logger);
        
        // act
        var result = await cmd.Run();
        
        // assert
        Assert.NotNull(result);
        Assert.IsType<IncrementalBuildResult>(result);
        var affectedProjects = ((IncrementalBuildResult) result).AffectedProjects;
        Assert.Contains(affectedProjects, p => p.Path.Contains("ProjectB.Tests"));
    }
    
    public Task InitializeAsync()
    {
        Repository
            .CreateBranch(PrimaryBranch)
            .CheckoutBranch(PrimaryBranch)
            .WriteSolution(_generatedTestSolution)
            .Commit("initial commit")
            .CreateBranch(SecondaryBranch)
            .CheckoutBranch(SecondaryBranch);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Repository.Dispose();
        return Task.CompletedTask;
    }
}
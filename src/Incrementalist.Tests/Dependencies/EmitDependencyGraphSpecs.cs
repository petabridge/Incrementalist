using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Cmd.Commands;
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
    private readonly SolutionModel _generatedSolution;
    private readonly ILogger _logger;
    
    private const string PrimaryBranch = "dev";
    private const string SecondaryBranch = "fixes";
    
    public DisposableRepository Repository { get; }
    
    public EmitDependencyGraphSpecs(ITestOutputHelper outputHelper, MSBuildFixture fixture)
    {
        _outputHelper = outputHelper;
        _workspace = fixture.Workspace;
        Repository = new DisposableRepository();
        _generatedSolution = CreateSolution();
        _logger = new TestOutputLogger(outputHelper);
    }

    private BuildSettings GetBuildSettings() =>
        new BuildSettings(PrimaryBranch, _generatedSolution.FileName, Repository.BasePath);

    private static SolutionModel CreateSolution()
    {
        var solutionBuilder = new SolutionBuilder("SampleSolution")
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
        var projectC = _generatedSolution.FlatProjects.Single(p => p.NameWithoutExtension == "ProjectB.Tests");
        Repository.AddOrModifyProjectFile(projectC, newFile).Commit("Added new file"); // should create the diffs
        var cmd = new EmitDependencyGraphTask(GetBuildSettings(), _workspace, _logger);
        
        // act
        var result = await cmd.Run();
        
        // assert
        Assert.NotNull(result);
        Assert.IsType<IncrementalBuildResult>(result);
    }
    
    public Task InitializeAsync()
    {
        Repository
            .CreateBranch(PrimaryBranch)
            .CheckoutBranch(PrimaryBranch)
            .WriteSolution(_generatedSolution)
            .Commit("initial commit")
            .CreateBranch(SecondaryBranch);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Repository.Dispose();
        return Task.CompletedTask;
    }
}
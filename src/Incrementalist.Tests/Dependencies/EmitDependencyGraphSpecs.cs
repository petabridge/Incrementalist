using System.IO;
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
        new BuildSettings(PrimaryBranch, _generatedTestSolution.FileName, Repository.BasePath){ NoCache = true };

    public const string ProjectBTests = "ProjectB.Tests";
    public const string ProjectB = "ProjectB";
    public const string ProjectA = "ProjectA";

    private static TestSolutionModel CreateSolution()
    {
        var solutionBuilder = new TestSolutionBuilder("SampleSolution")
            .AddFolder("src", f1Builder =>
            {
                f1Builder.AddProject(ProjectA,
                    (_, p1Builder) => { p1Builder.WithFile("HelloWorld.cs", CsharpSamples.HelloClass); });

                f1Builder.AddProject(ProjectB, (otherProjects, p2Builder) =>
                {
                    p2Builder.WithFile("GoodBye.cs", CsharpSamples.GoodbyeClassWithNamespace);
                    var projectA = otherProjects.First(p => p.NameWithoutExtension == ProjectA);
                    p2Builder.WithProjectReference(projectA);
                });
            })
            .AddFolder("test", f2Builder =>
            {
                f2Builder.AddProject(ProjectBTests, (otherProjects, p3Builder) =>
                {
                    p3Builder.WithFile("HelloWorldTests.cs", CsharpSamples.BarClass);
                    var projectB = otherProjects.First(p => p.NameWithoutExtension == ProjectB);
                    p3Builder.WithProjectReference(projectB);
                });
            })
            .Build();

        return solutionBuilder;
    }

    [Theory]
    [InlineData(ProjectBTests, new[]{ ProjectBTests })]
    [InlineData(ProjectB, new[]{ ProjectB, ProjectBTests })]
    [InlineData(ProjectA, new[]{ ProjectA, ProjectB, ProjectBTests })]
    public async Task ShouldDetectProjectCChanges(string projectToModify, string[] affectedProjects)
    {
        // arrange
        var newFile = new SampleFile("NewFile.cs", CsharpSamples.FooClass);
        var projectC = _generatedTestSolution.FlatProjects.Single(p => p.NameWithoutExtension == projectToModify);
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
        var actualAffectedProjects = ((IncrementalBuildResult)result).AffectedProjects
            .Select(c => Path.GetFileNameWithoutExtension(c.Path)).ToList();
        Assert.Equivalent(affectedProjects, actualAffectedProjects);
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
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Cmd;
using Incrementalist.Cmd.Commands;
using Incrementalist.Git;
using Incrementalist.ProjectSystem;
using Incrementalist.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Dependencies;

public abstract class EmitDependencyGraphSpecs : IAsyncLifetime
{
    public class Workspace(ITestOutputHelper outputHelper) : EmitDependencyGraphSpecs(outputHelper, logger => new WorkspaceBuildEngine(logger));

    public class StaticGraph(ITestOutputHelper outputHelper) : EmitDependencyGraphSpecs(outputHelper, logger => new StaticGraphBuildEngine(logger, verbose: false));

    private readonly BuildEngine _engine;
    private readonly TestSolutionModel _generatedTestSolution;
    private readonly ILogger _logger;

    private const string PrimaryBranch = "dev";
    private const string SecondaryBranch = "fixes";

    public DisposableRepository Repository { get; }

    protected EmitDependencyGraphSpecs(ITestOutputHelper outputHelper, Func<ILogger, BuildEngine> buildEngine)
    {
        Repository = new DisposableRepository();
        _generatedTestSolution = CreateSolution();
        _logger = new TestOutputLogger(outputHelper);
        _engine = buildEngine(_logger);
    }

    private BuildSettings GetBuildSettings() =>
        new BuildSettings(PrimaryBranch, _generatedTestSolution.FilePath, Repository.BasePath, [], [], "dotnet");

    public const string ProjectBTests = "ProjectB.Tests";
    public const string ProjectB = "ProjectB";
    public const string ProjectA = "ProjectA";
    public const string ProjectC = "ProjectC";

    private static TestSolutionModel CreateSolution()
    {
        var solutionBuilder = new TestSolutionBuilder("SampleSolution")
            .AddFolder("src", f1Builder =>
            {
                f1Builder.AddProject(ProjectA,
                    (_, p1Builder) =>
                    {
                        p1Builder.WithFile("HelloWorld.cs", CsharpSamples.HelloClass);
                        p1Builder.WithTargetFrameworks([TargetFramework.Net8, TargetFramework.Net9, TargetFramework.NetStandard2_0, TargetFramework.NetStandard2_1]);
                    });

                f1Builder.AddProject(ProjectB, (otherProjects, p2Builder) =>
                {
                    p2Builder.WithFile("GoodBye.cs", CsharpSamples.GoodbyeClassWithNamespace);
                    var projectA = otherProjects.First(p => p.NameWithoutExtension == ProjectA);
                    p2Builder.WithProjectReference(projectA);
                    p2Builder.WithTargetFrameworks([TargetFramework.Net8, TargetFramework.Net9, TargetFramework.NetStandard2_0, TargetFramework.NetStandard2_1]);
                });

                // a third project, C, with no references to anyone else
                f1Builder.AddProject(ProjectC,
                    (_, p1Builder) => { p1Builder.WithFile("HelloWorld.cs", CsharpSamples.HelloClass); });
            })
            .AddFolder("test", f2Builder =>
            {
                f2Builder.AddProject(ProjectBTests, (otherProjects, p3Builder) =>
                {
                    p3Builder.WithFile("HelloWorldTests.cs", CsharpSamples.BarClass);
                    var projectB = otherProjects.First(p => p.NameWithoutExtension == ProjectB);
                    p3Builder.WithProjectReference(projectB);
                    p3Builder.WithTargetFrameworks([TargetFramework.Net9]);
                });
            })
            .Build();

        return solutionBuilder;
    }

    [Theory]
    [InlineData(ProjectBTests, new[] { ProjectBTests })]
    [InlineData(ProjectB, new[] { ProjectB, ProjectBTests })]
    [InlineData(ProjectA, new[] { ProjectA, ProjectB, ProjectBTests })]
    public async Task ShouldDetectProjectChanges(string projectToModify, string[] affectedProjects)
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

        var cmd = new EmitDependencyGraphTask(GetBuildSettings(), _engine, _logger,
            CancellationToken.None);
        
        // act
        var result = await cmd.Run();

        // assert
        Assert.NotNull(result);
        Assert.IsType<IncrementalBuildResult>(result);
        var actualAffectedProjects = ((IncrementalBuildResult)result).AffectedProjects
            .Select(c => Path.GetFileNameWithoutExtension(c.Path)).ToList();
        Assert.Equivalent(affectedProjects, actualAffectedProjects);
    }

    [Theory]
    [InlineData("src/Directory.Build.props", SolutionFileSamples.DirectoryBuildProps)]
    [InlineData("Directory.Build.props", SolutionFileSamples.DirectoryBuildProps)]
    [InlineData("Directory.Packages.props", SolutionFileSamples.DirectoryPackagesProps)]
    public async Task ShouldDetectSolutionWideChanges(string fileName, string fileContent)
    {
        // arrange
        var newFile = new SampleFile(fileName, fileContent);
        Repository
            .WriteFile(newFile)
            .Commit("Added new file"); // should create the diffs

        // validate that we can detect the changes
        var diffs = DiffHelper.ChangedFiles(Repository.Repository, PrimaryBranch).ToList();
        Assert.NotEmpty(diffs);

        var cmd = new EmitDependencyGraphTask(GetBuildSettings(), _engine, _logger, CancellationToken.None);

        // act
        var result = await cmd.Run();

        // assert
        Assert.NotNull(result);
        Assert.IsType<FullSolutionBuildResult>(result);
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
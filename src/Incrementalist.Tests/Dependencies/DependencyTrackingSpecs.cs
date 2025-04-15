using System.Linq;
using System.Threading.Tasks;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis.MSBuild;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Dependencies;

[Collection(MSBuildCollectionFixture.Name)]
public class DependencyTrackingSpecs : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly MSBuildWorkspace _workspace;
    private readonly SolutionModel _generatedSolution;
    public DisposableRepository Repository { get; }
    
    public DependencyTrackingSpecs(ITestOutputHelper outputHelper, MSBuildFixture fixture)
    {
        _outputHelper = outputHelper;
        _workspace = fixture.Workspace;
        Repository = new DisposableRepository();
        _generatedSolution = CreateSolution();
    }

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
    
    public Task InitializeAsync()
    {
        Repository.WriteSolution(_generatedSolution);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Repository.Dispose();
        return Task.CompletedTask;
    }
}
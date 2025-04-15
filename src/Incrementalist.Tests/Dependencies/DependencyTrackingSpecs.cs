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
    public DisposableRepository Repository { get; }
    
    public DependencyTrackingSpecs(ITestOutputHelper outputHelper, MSBuildFixture fixture)
    {
        _outputHelper = outputHelper;
        _workspace = fixture.Workspace;
        Repository = new DisposableRepository();
    }
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Repository?.Dispose();
        return Task.CompletedTask;
    }
}
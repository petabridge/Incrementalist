using System;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Xunit;

namespace Incrementalist.Tests.Helpers
{
    [CollectionDefinition(Name)]
    public class MSBuildCollectionFixture : ICollectionFixture<MSBuildFixture>
    {
        public const string Name = "MSBuild Collection";
    }

    public class MSBuildFixture : IDisposable
    {
        public MSBuildFixture()
        {
            // Initialize MSBuild exactly once for all tests in this collection
            MSBuildLocator.RegisterDefaults();
            Workspace = MSBuildWorkspace.Create();
        }

        public MSBuildWorkspace Workspace { get; }

        public void Dispose()
        {
            Workspace?.Dispose();
        }
    }
} 
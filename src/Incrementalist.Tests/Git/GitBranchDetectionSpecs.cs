using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Incrementalist.Git;
using Incrementalist.Tests.Helpers;
using Xunit;

namespace Incrementalist.Tests.Git
{
    public class GitBranchDetectionSpecs : IDisposable
    {
        public GitBranchDetectionSpecs()
        {
            Repository = new DisposableRepository();
        }

        public void Dispose()
        {
            Repository?.Dispose();
        }

        public DisposableRepository Repository { get; }

        [Fact(DisplayName = "Should detect existing Git branch")]
        public void ShouldDetectExistingBranch()
        {
            Repository.CreateBranch("foo");
            DiffHelper.HasBranch(Repository.Repository, "foo").Should().BeTrue();
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="GitBranchDetectionSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
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
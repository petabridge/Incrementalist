// -----------------------------------------------------------------------
// <copyright file="GitRepositoryDetectionSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using FluentAssertions;
using Incrementalist.Git;
using Incrementalist.Tests.Helpers;
using Xunit;
using LibGit2Sharp;

namespace Incrementalist.Tests.Git
{
    public class GitRepositoryDetectionSpecs : IDisposable
    {
        public GitRepositoryDetectionSpecs()
        {
            Repository = new DisposableRepository();
        }

        public void Dispose()
        {
            Repository?.Dispose();
        }

        public DisposableRepository Repository { get; }

        [Fact(DisplayName = "GitRunner should detect repository correctly")]
        public void Should_detect_Repository()
        {
            var results = GitRunner.FindRepository(Repository.BasePath);
            results.foundRepo.Should().BeTrue();

            // note: due to what I believe is native interop here, the Repository.Info.WorkingDirectory
            // string appears to have an extra null terminator at the end
            results.repo.Info.WorkingDirectory.Should().Contain(Repository.BasePath.Trim());
        }
    }
}
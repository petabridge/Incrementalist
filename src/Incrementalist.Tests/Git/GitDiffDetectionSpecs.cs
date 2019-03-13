using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Incrementalist.Git;
using Incrementalist.Tests.Helpers;
using Xunit;

namespace Incrementalist.Tests.Git
{
    public class GitDiffDetectionSpecs : IDisposable
    {
        public DisposableRepository Repository { get; }

        public GitDiffDetectionSpecs()
        {
            Repository = new DisposableRepository();
        }

        [Fact(DisplayName = "Should detect files that have been added to git repo")]
        public void Should_detect_added_files()
        {
            Repository.CreateBranch("foo").CheckoutBranch("foo").WriteFile("fuber.txt", "fuber").Commit("Fuberized file");
            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            diffedFiles.Count.Should().Be(1);
            var file = diffedFiles[0];
            Path.GetFileName(file).Should().Be("fuber.txt");
        }

        public void Dispose()
        {
            Repository?.Dispose();
        }
    }
}

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
        public void Should_detect_added_files_to_bare_Repo()
        {
            Repository.CreateBranch("foo").CheckoutBranch("foo").WriteFile("fuber.txt", "fuber").Commit("Fuberized file");
            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            diffedFiles.Count.Should().Be(1);
            var file = diffedFiles[0];
            Path.GetFileName(file).Should().Be("fuber.txt");
        }

        [Fact(DisplayName = "Should detect files that have been modified in existing repo")]
        public void Should_detect_changes_to_existing_files_in_Repo()
        {
            Repository.WriteFile("fuber.txt", "fuber")
                .Commit("Fuberized file")
                .CreateBranch("foo")
                .CheckoutBranch("foo")
                .WriteFile("fuber.txt", "fuber2")
                .Commit("Updated fuberized file");

            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            diffedFiles.Count.Should().Be(1);
            var file = diffedFiles[0];
            Path.GetFileName(file).Should().Be("fuber.txt");
        }

        [Fact(DisplayName = "Should detect files that have been deleted in existing repo")]
        public void Should_detect_delete_of_existing_file_in_Repo()
        {
            Repository.WriteFile("fuber.txt", "fuber")
                .Commit("Fuberized file")
                .CreateBranch("foo")
                .CheckoutBranch("foo")
                .DeleteFile("fuber.txt")
                .Commit("Delete fuberized file");

            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            diffedFiles.Count.Should().Be(1);
            var file = diffedFiles[0];
            Path.GetFileName(file).Should().Be("fuber.txt");
        }

        [Fact(DisplayName = "Should not detect any changes when none are present")]
        public void Should_not_detect_any_changes_when_none_present()
        {
            Repository.CreateBranch("foo").CheckoutBranch("foo");

            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            diffedFiles.Count.Should().Be(0);
        }

        public void Dispose()
        {
            Repository?.Dispose();
        }
    }
}

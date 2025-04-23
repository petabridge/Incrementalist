// -----------------------------------------------------------------------
// <copyright file="GitDiffDetectionSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Incrementalist.Git;
using Incrementalist.Tests.Helpers;
using Xunit;

namespace Incrementalist.Tests.Git
{
    public class GitDiffDetectionSpecs : IDisposable
    {
        public GitDiffDetectionSpecs()
        {
            Repository = new DisposableRepository();
        }

        public void Dispose()
        {
            Repository?.Dispose();
        }

        public DisposableRepository Repository { get; }

        [Fact(DisplayName = "Should detect files that have been added to git repo")]
        public void Should_detect_added_files_to_bare_Repo()
        {
            Repository.CreateBranch("foo").CheckoutBranch("foo").WriteFile("fuber.txt", "fuber")
                .Commit("Fuberized file");
            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            Assert.Single(diffedFiles);
            var file = diffedFiles[0];
            Assert.Equal("fuber.txt", Path.GetFileName(file.Path));
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
            Assert.Single(diffedFiles);
            var file = diffedFiles[0];
            Assert.Equal("fuber.txt", Path.GetFileName(file.Path));
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
            Assert.Single(diffedFiles);
            var file = diffedFiles[0];
            Assert.Equal("fuber.txt", Path.GetFileName(file.Path));
        }

        [Fact(DisplayName = "Should detect unstaged changes in working directory")]
        public void Should_detect_unstaged_changes()
        {
            // Create and commit a file on master
            Repository.WriteFile("committed.txt", "committed")
                .Commit("Add committed file");

            // Create a branch and add a staged file
            Repository.CreateBranch("foo")
                .CheckoutBranch("foo")
                .WriteFile("staged.txt", "staged");

            // Create an unstaged file
            var unstagedPath = Path.Combine(Repository.BasePath.Path, "unstaged.txt");
            File.WriteAllText(unstagedPath, "unstaged");

            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            Assert.Equal(2, diffedFiles.Count);
            Assert.Contains(diffedFiles, f => Path.GetFileName(f.Path) == "staged.txt");
            Assert.Contains(diffedFiles, f => Path.GetFileName(f.Path) == "unstaged.txt");
        }

        [Fact(DisplayName = "Should not detect any changes when none are present")]
        public void Should_not_detect_any_changes_when_none_present()
        {
            Repository.CreateBranch("foo").CheckoutBranch("foo");

            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            Assert.Empty(diffedFiles);
        }

        [Fact(DisplayName = "Detected files should report correct absolute path")]
        public void Should_report_correct_path_for_diffed_files()
        {
            Repository.CreateBranch("foo").CheckoutBranch("foo").WriteFile("fuber.txt", "fuber")
                .Commit("Fuberized file");
            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, "master").ToList();
            Assert.Single(diffedFiles);
            var file = diffedFiles[0];
            Assert.Equal("fuber.txt", Path.GetFileName(file.Path));
            Assert.Equal(Path.GetFullPath("fuber.txt", Repository.BasePath.Path), file.Path);
        }

        [Fact(DisplayName = "Should detect changes when comparing against a commit SHA")]
        public void Should_detect_changes_against_commit_sha()
        {
            var initialCommit = Repository.WriteFile("file1.txt", "content1")
                .Commit("Initial commit");

            Repository.WriteFile("file2.txt", "content2")
                .Commit("Second commit");

            var diffedFiles = DiffHelper.ChangedFiles(Repository.Repository, initialCommit.Repository.Head.Tip.Sha).ToList();
            Assert.Single(diffedFiles);
            Assert.Equal("file2.txt", Path.GetFileName(diffedFiles[0].Path));
        }
    }
}
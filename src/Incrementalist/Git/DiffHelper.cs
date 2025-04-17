// -----------------------------------------------------------------------
// <copyright file="DiffHelper.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace Incrementalist.Git
{
    /// <summary>
    ///     Generate diffs for the current repository between branches
    /// </summary>
    public static class DiffHelper
    {
        public static IEnumerable<AbsolutePath> ChangedFiles(Repository repo, string targetBranch)
        {
            var targetTree = repo.Branches[targetBranch].Tip.Tree;
            var changes = new HashSet<AbsolutePath>();

            // Get all changes between target branch and current state (including both staged and unstaged)
            var status = repo.RetrieveStatus();

            // Add staged changes
            foreach (var staged in status.Staged)
            {
                changes.Add(
                    new AbsolutePath(Path.GetFullPath(Path.Combine(repo.Info.WorkingDirectory, staged.FilePath))));
            }

            // Add unstaged changes
            foreach (var unstaged in status.Modified.Concat(status.Added).Concat(status.Untracked))
            {
                changes.Add(
                    new AbsolutePath(Path.GetFullPath(Path.Combine(repo.Info.WorkingDirectory, unstaged.FilePath))));
            }

            // Add changes between target branch and HEAD
            var branchDiff = repo.Diff.Compare<TreeChanges>(targetTree, repo.Head.Tip.Tree);
            foreach (var change in branchDiff)
            {
                changes.Add(new AbsolutePath(Path.GetFullPath(Path.Combine(repo.Info.WorkingDirectory, change.Path))));
            }

            return changes;
        }

        public static bool HasBranch(Repository repo, string targetBranch)
        {
            return repo.Branches.Any(x => x.FriendlyName.Equals(targetBranch));
        }
    }
}
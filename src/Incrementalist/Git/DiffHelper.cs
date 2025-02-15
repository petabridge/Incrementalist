// -----------------------------------------------------------------------
// <copyright file="DiffHelper.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using System;

namespace Incrementalist.Git
{
    /// <summary>
    ///     Generate diffs for the current repository between branches
    /// </summary>
    public static class DiffHelper
    {
        public static IEnumerable<string> ChangedFiles(Repository repo, string targetBranch)
        {
            return repo.Diff.Compare<TreeChanges>(repo.Branches[targetBranch].Tip.Tree, DiffTargets.Index)
                .Select(x => Path.GetFullPath(Path.Combine(repo.Info.WorkingDirectory, x.Path)));
        }

        public static bool HasBranch(Repository repo, string targetBranch)
        {
            return repo.Branches.Any(x => x.FriendlyName.Equals(targetBranch));
        }

        /// <summary>
        /// Checks if the specified branch is the current branch.
        /// </summary>
        public static bool IsCurrentBranch(Repository repo, string targetBranch)
        {
            // Handle HEAD reference explicitly
            if (targetBranch.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                return true;

            // Normalize branch names by removing origin/ prefix if present
            var currentBranch = repo.Head.FriendlyName;
            var normalizedTarget = targetBranch.Replace("origin/", "");
            var normalizedCurrent = currentBranch.Replace("origin/", "");

            return normalizedCurrent.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase);
        }
    }
}
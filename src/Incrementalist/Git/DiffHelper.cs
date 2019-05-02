﻿// -----------------------------------------------------------------------
// <copyright file="DiffHelper.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
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
        public static IEnumerable<string> ChangedFiles(Repository repo, string targetBranch)
        {
            return repo.Diff.Compare<TreeChanges>(repo.Branches[targetBranch].Tip.Tree, DiffTargets.Index)
                .Select(x => Path.GetFullPath(Path.Combine(repo.Info.WorkingDirectory, x.Path)));
        }
    }
}
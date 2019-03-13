﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;

namespace Incrementalist.Git
{
    /// <summary>
    /// Generate diffs for the current repository between branches
    /// </summary>
    public static class DiffHelper
    {
        public static IEnumerable<string> ChangedFiles(Repository repo, string targetBranch, string workingDirectory = null)
        {
            var finalDir = workingDirectory ?? Directory.GetCurrentDirectory();
            return repo.Diff.Compare<TreeChanges>(repo.Branches[targetBranch].Tip.Tree, DiffTargets.Index).Select(x => Path.GetFullPath(x.Path));
        }
    }
}

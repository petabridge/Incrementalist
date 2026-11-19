// -----------------------------------------------------------------------
// <copyright file="ContainmentSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using Incrementalist.ProjectSystem.Cmds;
using Xunit;

namespace Incrementalist.Tests.ProjectSystem
{
    /// <summary>
    ///     Regression tests for https://github.com/petabridge/Incrementalist/issues/547
    ///     <para>
    ///         The old fallback enumerated only the immediate subdirectories of each project folder,
    ///         so a file sitting directly in a project directory never matched, and a file nested two
    ///         or more levels deep never matched either. It also returned only the first matching
    ///         project, so a file under a nested project was attributed to the outer project.
    ///     </para>
    /// </summary>
    public class ContainmentSpecs
    {
        private static string Root =>
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "incrementalist-containment"));

        private static string P(string relative) => Path.Combine(Root, relative);

        [Theory(DisplayName = "A file directly inside a project directory is contained by it")]
        [InlineData("src/Core/Strings.resx")]
        [InlineData("src/Core/Thing.cs")]
        [InlineData("src/Core/appsettings.json")]
        public void File_at_project_root_is_contained(string file)
        {
            Assert.True(FilterAffectedProjectFilesCmd.IsUnderDirectory(P(file), P("src/Core")));
        }

        [Theory(DisplayName = "A file nested arbitrarily deep is contained by the project directory")]
        [InlineData("src/Core/Resources/Strings.resx")]
        [InlineData("src/Core/Resources/en-US/Strings.resx")]
        [InlineData("src/Core/a/b/c/d/e/f.txt")]
        public void File_nested_deep_is_contained(string file)
        {
            Assert.True(FilterAffectedProjectFilesCmd.IsUnderDirectory(P(file), P("src/Core")));
        }

        [Theory(DisplayName = "A sibling directory sharing a name prefix is NOT contained")]
        [InlineData("src/CoreExtra/Thing.cs")]
        [InlineData("src/Core2/Thing.cs")]
        [InlineData("src/Core.Tests/Thing.cs")]
        public void Sibling_with_shared_prefix_is_not_contained(string file)
        {
            Assert.False(FilterAffectedProjectFilesCmd.IsUnderDirectory(P(file), P("src/Core")));
        }

        [Fact(DisplayName = "A file outside every project directory is not contained")]
        public void File_outside_project_is_not_contained()
        {
            Assert.False(FilterAffectedProjectFilesCmd.IsUnderDirectory(P("README.md"), P("src/Core")));
            Assert.False(FilterAffectedProjectFilesCmd.IsUnderDirectory(P("build/common.props"), P("src/Core")));
        }

        [Fact(DisplayName = "A nested project directory is contained by its ancestor")]
        public void Nested_project_directory_is_contained_by_ancestor()
        {
            // src/Outer/Inner is inside src/Outer, so a file there belongs to both projects.
            Assert.True(FilterAffectedProjectFilesCmd.IsUnderDirectory(P("src/Outer/Inner/Inner.cs"), P("src/Outer")));
            Assert.True(FilterAffectedProjectFilesCmd.IsUnderDirectory(P("src/Outer/Inner/Inner.cs"), P("src/Outer/Inner")));
        }

        [Theory(DisplayName = "Containment returns EVERY containing project, not just the first")]
        [InlineData("src/Outer", "src/Outer/Inner")]
        [InlineData("src/Outer/Inner", "src/Outer")]
        public void Containment_returns_all_containing_projects(string first, string second)
        {
            // Order matters: the old implementation returned on the first match, so a test that
            // only checks each directory independently would pass even if aggregation regressed.
            var folders = new[] { new AbsolutePath(P(first)), new AbsolutePath(P(second)) };
            var file = new AbsolutePath(P("src/Outer/Inner/InnerStrings.resx"));

            Assert.True(FilterAffectedProjectFilesCmd.TryFindContainingProjects(folders, file, out var matches));
            Assert.NotNull(matches);
            Assert.Equal(2, matches!.Count);
            Assert.Contains(matches, m => m.Path == P(first));
            Assert.Contains(matches, m => m.Path == P(second));
        }

        [Fact(DisplayName = "Containment returns a single project when only one contains the file")]
        public void Containment_returns_single_project_when_only_one_matches()
        {
            var folders = new[] { new AbsolutePath(P("src/Core")), new AbsolutePath(P("src/Unrelated2")) };
            var file = new AbsolutePath(P("src/Core/Resources/Strings.resx"));

            Assert.True(FilterAffectedProjectFilesCmd.TryFindContainingProjects(folders, file, out var matches));
            Assert.Single(matches!);
            Assert.Equal(P("src/Core"), matches![0].Path);
        }

        [Fact(DisplayName = "Containment returns false when no project contains the file")]
        public void Containment_returns_false_when_nothing_matches()
        {
            var folders = new[] { new AbsolutePath(P("src/Core")) };
            var file = new AbsolutePath(P("README.md"));

            Assert.False(FilterAffectedProjectFilesCmd.TryFindContainingProjects(folders, file, out var matches));
            Assert.Null(matches);
        }

        [Fact(DisplayName = "Containment does not require the file to exist")]
        public void Containment_is_lexical_and_does_not_require_existence()
        {
            // Deletions must still be attributed, so the check cannot touch the filesystem.
            var deleted = P("src/Core/Resources/en-US/Deleted.resx");
            Assert.False(File.Exists(deleted));
            Assert.True(FilterAffectedProjectFilesCmd.IsUnderDirectory(deleted, P("src/Core")));
        }

        [Fact(DisplayName = "Trailing separators are normalized")]
        public void Trailing_separators_are_normalized()
        {
            var withSeparator = P("src/Core") + Path.DirectorySeparatorChar;
            Assert.True(FilterAffectedProjectFilesCmd.IsUnderDirectory(P("src/Core/Thing.cs"), withSeparator));
        }

        [Fact(DisplayName = "A directory is contained by itself")]
        public void Directory_is_contained_by_itself()
        {
            Assert.True(FilterAffectedProjectFilesCmd.IsUnderDirectory(P("src/Core"), P("src/Core")));
        }
    }
}

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

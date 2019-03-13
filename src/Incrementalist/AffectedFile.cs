using System;
using System.Collections.Generic;
using System.Text;

namespace Incrementalist
{
    /// <summary>
    /// The types of files that can be detected automatically by Roslyn.
    /// </summary>
    public enum FileType
    {
        Code,
        Project,
        Solution,
        Script,
        Other
    }


    /// <summary>
    /// Used to document a file that was affected by the current commit.
    /// </summary>
    public sealed class AffectedFile
    {
        public AffectedFile(string path, FileType fileType, string project)
        {
            Path = path;
            FileType = fileType;
            Project = project;
        }

        /// <summary>
        /// The absolute path to the file.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The type of file detected in the diff.
        /// </summary>
        public FileType FileType { get; }

        /// <summary>
        /// The name of the affected project.
        /// </summary>
        public string Project { get; }
    }
}

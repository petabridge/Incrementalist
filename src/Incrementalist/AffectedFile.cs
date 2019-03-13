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
    public sealed class AffectedFile : IEquatable<AffectedFile>
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

        public bool Equals(AffectedFile other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Path, other.Path) && FileType == other.FileType && string.Equals(Project, other.Project);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is AffectedFile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Path != null ? Path.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int) FileType;
                hashCode = (hashCode * 397) ^ (Project != null ? Project.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(AffectedFile left, AffectedFile right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AffectedFile left, AffectedFile right)
        {
            return !Equals(left, right);
        }
    }
}

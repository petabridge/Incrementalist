using System.IO;

namespace Incrementalist.Tests.Helpers
{
    /// <summary>
    /// ProjectSampleGenerator
    /// </summary>
    public static class ProjectSampleGenerator
    {
        /// <summary>
        /// Gets project with import files sample
        /// </summary>
        public static ProjectWithImportSample GetProjectWithImportSample()
        {
            var projectContent = File.ReadAllText("../../../Samples/ProjectFileWithImportSample.xml");
            var importedPropsContent = File.ReadAllText("../../../Samples/ImportedPropsSample.xml");
            
            return new ProjectWithImportSample(projectContent, new SampleFile("imported.props", importedPropsContent));
        }

        /// <summary>
        /// Sample files required for tests with project imports
        /// </summary>
        public class ProjectWithImportSample
        {
            public ProjectWithImportSample(string projectFileContent, SampleFile importedPropsFile)
            {
                ProjectFileContent = projectFileContent;
                ImportedPropsFile = importedPropsFile;
            }

            /// <summary>
            /// Content of the project file that has an Import tag
            /// </summary>
            /// <remarks>
            /// Name of the file is not important here
            /// </remarks>
            public string ProjectFileContent { get; }
            /// <summary>
            /// Imported props file info
            /// </summary>
            /// <remarks>
            /// Make sure using same file name in tests - it is used in a project file content
            /// </remarks>
            public SampleFile ImportedPropsFile { get; }
        }

        /// <summary>
        /// Generated sample file info
        /// </summary>
        public class SampleFile
        {
            public SampleFile(string name, string content)
            {
                Name = name;
                Content = content;
            }

            /// <summary>
            /// Name of the file (might be used by another generated files)
            /// </summary>
            public string Name { get; }
            /// <summary>
            /// File content
            /// </summary>
            public string Content { get; }
        }
    }
}
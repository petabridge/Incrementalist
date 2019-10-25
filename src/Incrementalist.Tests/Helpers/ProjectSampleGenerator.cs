using System.IO;

namespace Incrementalist.Tests.Helpers
{
    /// <summary>
    /// ProjectSampleGenerator
    /// </summary>
    public static class ProjectSampleGenerator
    {
        /// <summary>
        /// Gets project file content
        /// </summary>
        public static string GetProjectFileContent()
        {
            return File.ReadAllText("../../../Samples/ProjectFileWithImportSample.xml");
        }

        /// <summary>
        /// Gets imported props file content
        /// </summary>
        public static string GetImportedPropsFileContent()
        {
            return File.ReadAllText("../../../Samples/ImportedPropsSample.xml");
        }
    }
}
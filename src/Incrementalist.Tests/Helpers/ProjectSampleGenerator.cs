using System;
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
        public static ProjectWithImportSample GetProjectWithImportSample(string projectFileName)
        {
            var projectContent = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../Samples/ProjectFileWithImportSample.xml"));
            var importedPropsContent = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../Samples/ImportedPropsSample.xml"));
            
            return new ProjectWithImportSample(
                new SampleFile(projectFileName, projectContent), 
                new SampleFile("imported.props", importedPropsContent));
        }

        /// <summary>
        /// Gets .net solution with different csharp and fsharp projects
        /// </summary>
        public static FSharpSampleSolution GetFSharpSolutionSample(string solutionName)
        {
            var solutionContent = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../Samples/FSharpSampleSolution/Solution.xml"));
            var fsharpProjectContent = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../Samples/FSharpSampleSolution/FSharpProject.xml"));
            var csharpProjectContent = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../Samples/FSharpSampleSolution/CSharpProject.xml"));
            
            return new FSharpSampleSolution(
                new SampleFile(solutionName, solutionContent), 
                new SampleFile("CSharpProject.csproj", csharpProjectContent), 
                new SampleFile("FSharpProject.fsproj", fsharpProjectContent));
        }

        /// <summary>
        /// Sample files required for tests with project imports
        /// </summary>
        public class ProjectWithImportSample
        {
            public ProjectWithImportSample(SampleFile projectFile, SampleFile importedPropsFile)
            {
                ProjectFile = projectFile;
                ImportedPropsFile = importedPropsFile;
            }

            /// <summary>
            /// Content of the project file that has an Import tag
            /// </summary>
            /// <remarks>
            /// Name of the file is not important here
            /// </remarks>
            public SampleFile ProjectFile { get; }
            /// <summary>
            /// Imported props file info
            /// </summary>
            /// <remarks>
            /// Make sure using same file name in tests - it is used in a project file content
            /// </remarks>
            public SampleFile ImportedPropsFile { get; }
        }
        
        /// <summary>
        /// FSharp sample solution data
        /// </summary>
        public class FSharpSampleSolution
        {
            public FSharpSampleSolution(SampleFile solutionFile, SampleFile fSharpProjectFile, SampleFile cSharpProjectFile)
            {
                SolutionFile = solutionFile;
                FSharpProjectFile = fSharpProjectFile;
                CSharpProjectFile = cSharpProjectFile;
            }

            /// <summary>
            /// Solution file info.
            /// </summary>
            public SampleFile SolutionFile { get; }
            /// <summary>
            /// FSharp project file info. Name of the project is used in solution's content
            /// </summary>
            public SampleFile FSharpProjectFile { get; }
            /// <summary>
            /// CSharp project file info. Name of the project is used in solution's content
            /// </summary>
            public SampleFile CSharpProjectFile { get; }
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

            /// <summary>
            /// Gets full file path
            /// </summary>
            public string GetFullPath(string basePath) => Path.Combine(basePath, Name);
        }
    }
}
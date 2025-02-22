#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        /// Creates a sample solution file content
        /// </summary>
        public static string CreateSolutionFile(string solutionName, IEnumerable<string> projectNames)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            sb.AppendLine("# Visual Studio Version 17");
            sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
            sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

            foreach (var projectName in projectNames)
            {
                var projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
                sb.AppendLine($"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{projectName}\", \"src\\{projectName}\\{projectName}.csproj\", \"{projectGuid}\"");
                sb.AppendLine("EndProject");
            }

            sb.AppendLine("Global");
            sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
            sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
            sb.AppendLine("\tEndGlobalSection");
            sb.AppendLine("EndGlobal");

            return sb.ToString();
        }

        /// <summary>
        /// Creates a sample project file content
        /// </summary>
        public static string CreateProjectFile(
            string projectName,
            IEnumerable<string>? projectReferences = null,
            string targetFramework = "net7.0")
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <TargetFramework>{targetFramework}</TargetFramework>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("  </PropertyGroup>");

            if (projectReferences?.Any() == true)
            {
                sb.AppendLine("  <ItemGroup>");
                foreach (var reference in projectReferences)
                {
                    sb.AppendLine($"    <ProjectReference Include=\"..\\{reference}\\{reference}.csproj\" />");
                }
                sb.AppendLine("  </ItemGroup>");
            }

            sb.AppendLine("</Project>");

            return sb.ToString();
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
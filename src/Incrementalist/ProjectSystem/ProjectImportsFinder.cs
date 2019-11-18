using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace Incrementalist.ProjectSystem
{
    public sealed class SlnFileWithPath
    {
        public SlnFileWithPath(string path, SlnFile file)
        {
            Path = path;
            File = file;
        }

        public string Path { get; }
        public SlnFile File { get; }
    }
    
    public sealed class ImportedFile
    {
        public ImportedFile(string path, IImmutableList<SlnFileWithPath> dependantProjects)
        {
            DependantProjects = dependantProjects;
            Path = path;
        }

        /// <summary>
        /// List of project files that are importing this file
        /// </summary>
        public IImmutableList<SlnFileWithPath> DependantProjects { get; }
        /// <summary>
        /// File path
        /// </summary>
        public string Path { get; }
    }
    
    /// <summary>
    /// Helps finding all imported files paths and their dependant projects
    /// </summary>
    public static class ProjectImportsFinder
    {
        /// <summary>
        /// Finds all files that were imported to specified projec files
        /// </summary>
        /// <param name="projectFiles">List of project files with their paths</param>
        /// <returns>Doctionary of imported files with their paths</returns>
        public static Dictionary<string, ImportedFile> FindProjectImports(IEnumerable<SlnFileWithPath> projectFiles)
        {
            var imports = new ConcurrentDictionary<string, IImmutableList<SlnFileWithPath>>();
            
            Parallel.ForEach(projectFiles, projectFile =>
            {
                if (projectFile.File.FileType != FileType.Project)
                    return;

                var xmlDoc = ParseXmlDocument(projectFile.Path);
                
                var projectDir = Path.GetDirectoryName(projectFile.Path);

                // Collecting all projects that contain imports, like <Import Project="../../common.props">
                var importTags = xmlDoc.DocumentElement.SelectNodes("//Import");
                foreach (XmlNode importTag in importTags)
                {
                    var importedFilePath = importTag.Attributes?.GetNamedItem("Project")?.Value;
                    if (string.IsNullOrEmpty(importedFilePath))
                        continue;
                    
                    var importedFileFillPath = Path.GetFullPath(Path.Combine(projectDir, importedFilePath));
                    imports.AddOrUpdate(importedFileFillPath,
                        addValue: ImmutableList<SlnFileWithPath>.Empty.Add(projectFile), 
                        updateValueFactory: (_, dependentProjects) => dependentProjects.Add(projectFile));
                }
            });

            return imports.ToDictionary(pair => pair.Key, pair => new ImportedFile(pair.Key, pair.Value));
        }

        private static XmlDocument ParseXmlDocument(string projectFilePath)
        {
            var fileContent = File.ReadAllText(projectFilePath);
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(fileContent);
            return xmlDoc;
        }
    }
}
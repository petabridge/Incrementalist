using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Incrementalist.Tests.Helpers;

    public enum ProjectLanguage
    {
        CSharp,
        FSharp
    }
    
    public enum OutputType
    {
        Exe,
        Library
    }
    
    public enum TargetFramework
    {
        Net7,
        Net8,
        Net9,
        NetStandard2_0,
        NetStandard2_1
    }

    public interface IMsBuildSerializable
    {
        string Serialize();
    }

    public sealed record TargetFrameworks(ImmutableList<TargetFramework> Frameworks) : IMsBuildSerializable
    {

        public string Serialize()
        {
            return Frameworks.Count switch
            {
                // throw an exception if the list is empty
                0 => throw new InvalidOperationException("TargetFrameworks cannot be empty."),
                // need to serialize this to either be a TargetFramework or a TargetFrameworks tag
                // depending on the number of frameworks
                1 => $"<TargetFramework>{GetFrameworkString(Frameworks[0])}</TargetFramework>",
                _ => "<TargetFrameworks>" + string.Join(";", Frameworks.Select(GetFrameworkString)) +
                     "</TargetFrameworks>"
            };
        }

        public static string GetFrameworkString(TargetFramework framework)
        {
            return framework switch
            {
                TargetFramework.Net7 => "net7.0",
                TargetFramework.Net8 => "net8.0",
                TargetFramework.Net9 => "net9.0",
                TargetFramework.NetStandard2_0 => "netstandard2.0",
                TargetFramework.NetStandard2_1 => "netstandard2.1",
                _ => throw new ArgumentOutOfRangeException(nameof(framework), framework, null)
            };
        }
    }

    /// <summary>
    /// Used to construct a MSBuild project
    /// </summary>
    public sealed class ProjectBuilder
    {
        private readonly ProjectId _projectId;
        private readonly string _basePath;
        private readonly string _nameWithoutExtension;
        
        private readonly HashSet<IProjectModelProperty> _projectProperties = [];
        private readonly HashSet<ProjectImport> _projectImports = [];
        private readonly Dictionary<ProjectId, string> _projectReferences = new();
        private readonly List<SampleFile> _includedFiles = [];
        
        private ProjectLanguage _language = ProjectLanguage.CSharp;
        private OutputType _projectType = OutputType.Library;
        
        // provide a default framework
        private TargetFrameworks _targetFrameworks = new(ImmutableList<TargetFramework>.Empty.Add(TargetFramework.Net8));
        
        private bool _isBuilt;
        
        private ProjectModel BuildInternal()
        {
            if (_isBuilt)
            {
                throw new InvalidOperationException("Project has already been built.");
            }
            _isBuilt = true;
            
            if(_projectProperties.Count == 0) // need to add our default property for output type
                _projectProperties.Add(new OutputTypeProperty(_projectType));
            
            return new ProjectModel(
                _projectId,
                _basePath,
                _nameWithoutExtension)
            {
                ProjectProperties = _projectProperties,
                ProjectReferences = _projectReferences,
                IncludedFiles = _includedFiles,
                ProjectLanguage = _language,
                ProjectType = _projectType,
                TargetFrameworks = _targetFrameworks,
                ProjectImports = _projectImports
            };
        }

        public ProjectBuilder(ProjectId projectId, string basePath, string nameWithoutExtension)
        {
            _projectId = projectId;
            _basePath = basePath;
            _nameWithoutExtension = nameWithoutExtension;
        }
        
        public ProjectBuilder WithProjectLanguage(ProjectLanguage language)
        {
            _language = language;
            return this;
        }
        
        public ProjectBuilder WithProjectType(OutputType projectType)
        {
            _projectType = projectType;
            return WithProjectProperty(new OutputTypeProperty(projectType));
        }
        
        public ProjectBuilder WithTargetFrameworks(IEnumerable<TargetFramework> targetFrameworks)
        {
            _targetFrameworks = new TargetFrameworks(targetFrameworks.ToImmutableList());
            return this;
        }
        
        public ProjectBuilder WithTargetFramework(TargetFramework targetFramework)
        {
            _targetFrameworks = new TargetFrameworks(ImmutableList<TargetFramework>.Empty.Add(targetFramework));
            return this;
        }
        
        public ProjectBuilder WithProjectProperty(IProjectModelProperty projectProperty)
        {
            _projectProperties.Add(projectProperty);
            return this;
        }
        
        public ProjectBuilder WithProjectReference(ProjectId projectId, string relativePath)
        {
            _projectReferences[projectId] = relativePath;
            return this;
        }
        
        public ProjectBuilder WithProjectImport(string relativePath)
        {
            var projectImport = new ProjectImport(relativePath);
            _projectImports.Add(projectImport);
            return this;
        }
        
        public ProjectBuilder WithFile(SampleFile file)
        {
            _includedFiles.Add(file);
            return this;
        }
        
        public ProjectBuilder WithFile(string relativeToProjectFilePathWithExtension, string fileText)
        {
            var sampleFile = new SampleFile(relativeToProjectFilePathWithExtension, fileText);
            _includedFiles.Add(sampleFile);
            return this;
        }
        
        public ProjectBuilder WithLanguage(ProjectLanguage language)
        {
            _language = language;
            return this;
        }
        
        public ProjectModel Build() => BuildInternal();
    }

    public static class ProjectModelSerializer
    {
        public static string Serialize(ProjectModel projectModel)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine($"  <PropertyGroup>");
            sb.AppendLine($"    {projectModel.TargetFrameworks.Serialize()}");
            foreach(var p in projectModel.ProjectProperties)
            {
                sb.AppendLine($"    {p.Serialize()}");
            }
            sb.AppendLine($"  </PropertyGroup>");
            
            foreach (var projectImport in projectModel.ProjectImports)
            {
                sb.AppendLine($"  <Import Project=\"{projectImport.RelativePath}\" />");
            }

            if (projectModel.ProjectReferences.Count > 0)
            {
                sb.AppendLine($"  <ItemGroup>");
                foreach (var projectReference in projectModel.ProjectReferences)
                {
                    sb.AppendLine($"  <ProjectReference Include=\"{projectReference.Value}\" />");
                }
                sb.AppendLine($"  </ItemGroup>");
                sb.AppendLine($"</Project>");
            }
            
            return sb.ToString();
        }
    }
    
    public sealed record ProjectModel(ProjectId ProjectId, string RelativePathFromRepository, string NameWithoutExtension) : IMsBuildSerializable
    {
        public ProjectLanguage ProjectLanguage { get; init; } = ProjectLanguage.CSharp;
        
        public required TargetFrameworks TargetFrameworks { get; init; }

        public OutputType ProjectType { get; init; } = OutputType.Library;

        /// <summary>
        /// All the arbitrary properties that are set in the project file
        /// </summary>
        /// <remarks>
        /// The left-hand string is the name of the property, the right-hand string is XML-serialized value.
        /// </remarks>
        public required IReadOnlyCollection<IProjectModelProperty> ProjectProperties { get; init; }
        
        /// <summary>
        /// A set of project imports that are used in this project
        /// </summary>
        public required IReadOnlyCollection<ProjectImport> ProjectImports { get; init; }
        
        /// <summary>
        /// All dependencies of this project - expressed as a dictionary of projectId -> relative path
        /// </summary>
        public required IReadOnlyDictionary<ProjectId, string> ProjectReferences { get; init; }
        
        public required IReadOnlyCollection<SampleFile> IncludedFiles { get; init; }
        
        public string FileExtension => ProjectLanguage switch
        {
            ProjectLanguage.CSharp => ".csproj",
            ProjectLanguage.FSharp => ".fsproj",
            _ => throw new ArgumentOutOfRangeException(nameof(ProjectLanguage), ProjectLanguage, null)
        };
        
        public string FileName => $"{NameWithoutExtension}{FileExtension}";
        
        public string CompletePath => Path.Combine(RelativePathFromRepository, FileName);
        
        public string Serialize()
        {
            return ProjectModelSerializer.Serialize(this);
        }
    }
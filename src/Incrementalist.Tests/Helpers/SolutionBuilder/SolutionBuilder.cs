using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Incrementalist.Tests.Helpers;

public sealed class SolutionBuilder
{
    private readonly string _baseDirectory;
    private readonly string _name;
    
    private readonly HashSet<ProjectModel> _projects = [];
    private readonly HashSet<IMsBuildSerializable> _items = [];

    public SolutionBuilder(string name, string baseDirectory = "")
    {
        _baseDirectory = baseDirectory;
        _name = name;
    }
    
    public SolutionBuilder AddFolder(string folderName, Action<SolutionFolderBuilder> builder)   
    {
        var folderBuilder = new SolutionFolderBuilder(folderName, this);
        builder(folderBuilder);
        var folder = folderBuilder.Build();
        _items.Add(folder);
        return this;
    }

    public SolutionBuilder AddProject(string projectName,
        Action<IReadOnlyCollection<ProjectModel>, ProjectBuilder> builder)
    {
        var projectId = ProjectId.CreateNewId();
        
        // each project gets its own directory
        var projectRelativePath = Path.Combine(_baseDirectory, projectName);
        var projectModel = new ProjectBuilder(projectId, projectRelativePath, projectName);
        builder(_projects, projectModel);
        var project = projectModel.Build();
        
        _items.Add(project);
        _projects.Add(project);
        return this;
    }
    
    public SolutionModel Build()
    {
        var flatProjects = _projects.ToImmutableHashSet();
        
        return new SolutionModel(_name, _baseDirectory)
        {
            FileStructure = _items.ToImmutableHashSet(),
            FlatProjects = flatProjects
        };
    }
    
    public sealed class SolutionFolderBuilder
    {
        private readonly string _name;
        private readonly SolutionBuilder _builder;
        private readonly List<IMsBuildSerializable> _items = [];
        
        public string CompleteRelativePath => Path.Combine(_builder._baseDirectory, _name);

        public SolutionFolderBuilder(string name, SolutionBuilder solutionBuilder)
        {
            _name = name;
            _builder = solutionBuilder;
        }
        
        public SolutionFolderBuilder AddProject(string projectName, Action<IReadOnlyCollection<ProjectModel>, ProjectBuilder> builder)
        {
            var projectId = ProjectId.CreateNewId();
            
            // each project gets its own directory
            var projectRelativePath = Path.Combine(CompleteRelativePath, projectName);
            var projectModel = new ProjectBuilder(projectId, projectRelativePath, projectName);
            builder(_builder._projects, projectModel);
            var project = projectModel.Build();
            
            _items.Add(project);
            _builder._projects.Add(project);
            return this;
        }
        
        public SolutionFolderBuilder AddFile(string fileName)
        {
            var file = new SampleFile(fileName, CompleteRelativePath);
            _items.Add(file);
            return this;
        }
        
        public SolutionFolderBuilder AddFolder(string folderName, Action<SolutionFolderBuilder> builder)
        {
            var newName = Path.Combine(_name, folderName);
            
            var folderBuilder = new SolutionFolderBuilder(newName, _builder);
            builder(folderBuilder);
            var folder = folderBuilder.Build();
            _items.Add(folder);
            return this;
        }
        
        public SolutionFolder Build()
        {
            return new SolutionFolder(_name, _items.ToImmutableList());
        }
    }
}

public sealed record SolutionModel(string Name, string BaseDirectory) : IMsBuildSerializable
{
    /// <summary>
    /// Flat set of all projects
    /// </summary>
    public ImmutableHashSet<ProjectModel> FlatProjects { get; init; } = ImmutableHashSet<ProjectModel>.Empty;
    
    /// <summary>
    /// The set of all folders, files, and projects organized by folder
    /// </summary>
    public ImmutableHashSet<IMsBuildSerializable> FileStructure { get; init; } = ImmutableHashSet<IMsBuildSerializable>.Empty;
    
    public string FileName => $"{Name}.slnx";
    
    public string FilePath => Path.Combine(BaseDirectory, FileName);
    public string Serialize()
    {
        return SolutionSerializer.Serialize(this);
    }
}

public static class SolutionSerializer
{
    public static string Serialize(SolutionModel solution)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<Solution>");
        foreach (var item in solution.FileStructure)
        {
            switch (item)
            {
                case ProjectModel projectModel:
                    sb.AppendLine($"<Project Path=\"{projectModel.CompletePath}\" />");
                    break;
                case SolutionFolder folder:
                    sb.AppendLine(folder.Serialize());
                    break;
                case SampleFile sampleFile:
                    sb.AppendLine($"<File Path=\"{sampleFile.Name}\" />");
                    break;
            }
        }
        sb.AppendLine($"</Solution>");

        return sb.ToString();
    }
}

public sealed record SolutionFolder(string Name, ImmutableList<IMsBuildSerializable> Items) : IMsBuildSerializable
{
    public string Serialize()
    {
        return SolutionFolderSerializer.Serialize(this);
    }
}

public static class SolutionFolderSerializer
{
    public static string Serialize(SolutionFolder folder)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<Folder Name=\"/{folder.Name.Trim('/')}/\"");

        foreach (var item in folder.Items)
        {
            switch (item)
            {
                case SampleFile sampleFile:
                    sb.AppendLine($"<File Path=\"{sampleFile.Name}\" />");
                    break;
                case ProjectModel projectModel:
                    sb.AppendLine($"<Project Path=\"{projectModel.CompletePath}\" />");
                    break;
            }
        }
        
        sb.AppendLine("</Folder>");
        return sb.ToString();
    }
}
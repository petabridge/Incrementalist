// -----------------------------------------------------------------------
// <copyright file="SolutionBuilder.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Incrementalist.Tests.Helpers;

public enum SolutionFormat
{
    Sln,
#if NET9_0_OR_GREATER
    Slnx
#endif
}

public sealed class TestSolutionBuilder
{
    private readonly RelativePath _baseDirectory;
    private readonly string _name;

    private readonly HashSet<ProjectModel> _projects = [];
    private readonly HashSet<IMsBuildSerializable> _items = [];
    private SolutionFormat _solutionFormat = SolutionFormat.Sln;

    public TestSolutionBuilder(string name, string baseDirectory = "")
    {
        _baseDirectory = new RelativePath(baseDirectory);
        _name = name;
    }

    public TestSolutionBuilder AddFolder(string folderName, Action<SolutionFolderBuilder> builder)
    {
        var folderBuilder = new SolutionFolderBuilder(folderName, this);
        builder(folderBuilder);
        var folder = folderBuilder.Build();
        _items.Add(folder);
        return this;
    }

    public TestSolutionBuilder AddProject(string projectName,
        Action<IReadOnlyCollection<ProjectModel>, ProjectBuilder> builder)
    {
        var projectId = ProjectId.CreateNewId();

        // each project gets its own directory
        var projectRelativePath = Path.Combine(_baseDirectory.Path, projectName);
        var projectModel = new ProjectBuilder(projectId, projectRelativePath, projectName);
        builder(_projects, projectModel);
        var project = projectModel.Build();

        _items.Add(project);
        _projects.Add(project);
        return this;
    }

    public TestSolutionBuilder WithSolutionFormat(SolutionFormat solutionFormat)
    {
        _solutionFormat = solutionFormat;
        return this;
    }

    public TestSolutionModel Build()
    {
        var flatProjects = _projects.ToImmutableHashSet();

        return new TestSolutionModel(_name, _baseDirectory)
        {
            FileStructure = _items.ToImmutableHashSet(),
            FlatProjects = flatProjects,
            FileFormat = _solutionFormat
        };
    }

    public sealed class SolutionFolderBuilder
    {
        private readonly string _name;
        private readonly TestSolutionBuilder _builder;
        private readonly List<IMsBuildSerializable> _items = [];

        public string CompleteRelativePath => Path.Combine(_builder._baseDirectory.Path, _name);

        public SolutionFolderBuilder(string name, TestSolutionBuilder testSolutionBuilder)
        {
            _name = name;
            _builder = testSolutionBuilder;
        }

        public SolutionFolderBuilder AddProject(string projectName,
            Action<IReadOnlyCollection<ProjectModel>, ProjectBuilder> builder)
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

public sealed record TestSolutionModel(string Name, RelativePath BaseDirectory) : IMsBuildSerializable
{
    /// <summary>
    /// Flat set of all projects
    /// </summary>
    public ImmutableHashSet<ProjectModel> FlatProjects { get; init; } = ImmutableHashSet<ProjectModel>.Empty;

    /// <summary>
    /// The set of all folders, files, and projects organized by folder
    /// </summary>
    public ImmutableHashSet<IMsBuildSerializable> FileStructure { get; init; } =
        ImmutableHashSet<IMsBuildSerializable>.Empty;

    public SolutionFormat FileFormat { get; init; } = SolutionFormat.Sln;

    public FileName FileName => FileFormat switch
    {
        SolutionFormat.Sln => new FileName($"{Name}.sln"),
#if NET9_0_OR_GREATER
        SolutionFormat.Slnx => new FileName($"{Name}.slnx"),
#endif
        _ => throw new ArgumentOutOfRangeException(nameof(SolutionFormat))
    };

    public RelativePath FilePath => new(Path.Combine(BaseDirectory.Path, FileName.Name));

    public string Serialize()
    {
        return SolutionSerializer.Serialize(this);
    }
}

public static class SolutionSerializer
{
    public static string Serialize(TestSolutionModel testSolution)
    {
        var solutionModel = new SolutionModel();

        solutionModel.AddPlatform("Any CPU");
        solutionModel.AddBuildType("Debug");
        solutionModel.AddBuildType("Release");

        foreach (var item in testSolution.FlatProjects)
        {
            AddItemToSolution(item);
        }

        return testSolution.FileFormat switch
        {
            SolutionFormat.Sln => SerializeSlnAsync(solutionModel).Result,
#if NET9_0_OR_GREATER
            SolutionFormat.Slnx => SerializeSlnxAsync(solutionModel).Result,
#endif
            _ => throw new ArgumentOutOfRangeException(nameof(testSolution.FileFormat))
        };

        void AddItemToSolution(IMsBuildSerializable item)
        {
            switch (item)
            {
                case ProjectModel projectModel:
                    var projectType = projectModel.ProjectLanguage switch
                    {
                        ProjectLanguage.CSharp => "C#",
                        ProjectLanguage.FSharp => "F#",
                        _ => throw new ArgumentOutOfRangeException(nameof(projectModel.ProjectType))
                    };
                    solutionModel.AddProject(projectModel.CompletePath, projectType);
                    break;
                case SolutionFolder folder:
                    solutionModel.AddFolder("/" + folder.Name + "/");
                    foreach (var subItem in folder.Items)
                    {
                        AddItemToSolution(subItem);
                    }

                    break;
            }
        }
    }

    public static async Task<string> SerializeSlnAsync(SolutionModel testSolution)
    {
        using var memoryStream = new MemoryStream();
        await SolutionSerializers.SlnFileV12.SaveAsync(memoryStream, testSolution, CancellationToken.None);

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

#if NET9_0_OR_GREATER
    public static async Task<string> SerializeSlnxAsync(SolutionModel testSolution)
    {
        using var memoryStream = new MemoryStream();
        await SolutionSerializers.SlnXml.SaveAsync(memoryStream, testSolution, CancellationToken.None);

        return Encoding.UTF8.GetString(memoryStream.ToArray());

        // var sb = new StringBuilder();
        // sb.AppendLine($"<Solution>");
        // foreach (var item in testSolution.FileStructure)
        // {
        //     switch (item)
        //     {
        //         case ProjectModel projectModel:
        //             sb.AppendLine($"<Project Path=\"{projectModel.CompletePath}\" />");
        //             break;
        //         case SolutionFolder folder:
        //             sb.AppendLine(folder.Serialize());
        //             break;
        //         case SampleFile sampleFile:
        //             sb.AppendLine($"<File Path=\"{sampleFile.Name}\" />");
        //             break;
        //     }
        // }
        // sb.AppendLine($"</Solution>");
        //
        // return sb.ToString();
    }
#endif
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
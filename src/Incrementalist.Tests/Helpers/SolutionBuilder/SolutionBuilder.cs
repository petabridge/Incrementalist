using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Incrementalist.Tests.Helpers;

public class SolutionBuilder
{
    
}

public sealed record SolutionModel(string Name, string BaseDirectory) : IMsBuildSerializable
{
    /// <summary>
    /// Flat set of all projects
    /// </summary>
    public ImmutableDictionary<ProjectId, ProjectModel> FlatProjects { get; init; } = ImmutableDictionary<ProjectId, ProjectModel>.Empty;
    
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
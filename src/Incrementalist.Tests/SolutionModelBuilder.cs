using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Incrementalist.Tests;

public enum SolutionFormat
{
    Sln,
    Slnx
}

public enum ProjectTypes
{
    CSharp,
    FSharp,
    VisualBasic
}

public sealed class SolutionModelBuilder
{
    public SolutionModelBuilder(string basePath, string solutionName, SolutionFormat format)
    {
        BasePath = basePath;
        SolutionName = solutionName;
        Format = format;

        _solutionModel = new SolutionModel();
    }

    public string BasePath { get; }
    public string SolutionName { get; }
    public SolutionFormat Format { get; }
    
    private readonly SolutionModel _solutionModel;
    
    public string GetAbsoluteSolutionPath()
    {
        return Format switch
        {
            SolutionFormat.Sln => $"{BasePath}/{SolutionName}.sln",
            SolutionFormat.Slnx => $"{BasePath}/{SolutionName}.slnx",
            _ => throw new System.NotSupportedException($"Unsupported solution format: {Format}")
        };
    }

    public const string CsharpTypeName = "C#";
    public const string VbTypeName = "VB";
    public const string FsharpTypeName = "F#";

    public SolutionProjectModel AddProject(string filePath, ProjectTypes projectType)
    {
        var typeName = projectType switch
        {
            ProjectTypes.CSharp => CsharpTypeName,
            ProjectTypes.VisualBasic => VbTypeName,
            ProjectTypes.FSharp => FsharpTypeName,
            _ => throw new NotSupportedException($"Unsupported project type: {projectType}")
        };

        var newProject = _solutionModel.AddProject(filePath, typeName);
        return newProject;
    }
    
    public async Task SaveAsync(CancellationToken ct = default)
    {
        ISolutionSerializer serializer = Format switch
        {
            SolutionFormat.Sln => SolutionSerializers.SlnFileV12,
            SolutionFormat.Slnx => SolutionSerializers.SlnXml,
            _ => throw new System.NotSupportedException($"Unsupported solution format: {Format}")
        };
        
        await serializer.SaveAsync(GetAbsoluteSolutionPath(), _solutionModel, ct);
        
    }
}
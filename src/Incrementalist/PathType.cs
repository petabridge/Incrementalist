using System;

namespace Incrementalist;

public enum PathType
{
    /// <summary>
    /// Just a file name - no path information of any kind
    /// </summary>
    FileName,
    RelativePath,
    AbsolutePath,
}

public interface IHavePathType
{
    /// <summary>
    /// Type of the path
    /// </summary>
    PathType PathType { get; }
}

public sealed record FileName(string Name) : IHavePathType
{
    public PathType PathType => PathType.FileName;

    public override string ToString() => Name;
}

public sealed record RelativePath : IHavePathType
{
    public RelativePath(string path)
    {
        if(System.IO.Path.IsPathFullyQualified(path))
            throw new ArgumentException("Path is not relative", nameof(path));
        Path = path;
    }
    
    public string Path { get; init; }
    
    public PathType PathType => PathType.RelativePath;
    
    public AbsolutePath ComputeAbsolutePath(string basePath)
    {
        var absolutePath = System.IO.Path.Combine(basePath, Path);
        return new AbsolutePath(absolutePath);
    }

    public override string ToString() => Path;
}

public sealed record AbsolutePath : IHavePathType
{
    public AbsolutePath(string path)
    {
        if(!System.IO.Path.IsPathFullyQualified(path))
            throw new ArgumentException("Path is not asbolute", nameof(path));
        Path = path;
    }

    public string Path { get; init; }
    
    public PathType PathType => PathType.RelativePath;
    
    public RelativePath ComputeRelativePath(string basePath)
    {
        var relativePath = System.IO.Path.GetRelativePath(basePath, Path);
        return new RelativePath(relativePath);
    }
    
    public override string ToString() => Path;
}
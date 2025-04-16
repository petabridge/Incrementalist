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
            throw new ArgumentException($"Path [{path}] is not relative", nameof(path));
        Path = path;
    }
    
    public static RelativePath Empty => new(".");
    
    public string Path { get; init; }
    
    public PathType PathType => PathType.RelativePath;
    
    public AbsolutePath ComputeAbsolutePath(AbsolutePath basePath)
    {
        var absolutePath = System.IO.Path.Combine(basePath.Path, Path);
        return new AbsolutePath(absolutePath);
    }

    public override string ToString() => Path;
}

public sealed record AbsolutePath : IHavePathType
{
    public AbsolutePath(string path)
    {
        // Linux paths can't work with IsFullyQualified
        if(!System.IO.Path.IsPathRooted(path))
            throw new ArgumentException($"Path [{path}] is not absolute", nameof(path));
        Path = path;
    }

    public string Path { get; init; }
    
    public PathType PathType => PathType.RelativePath;
    
    public RelativePath ComputeRelativePathToMe(AbsolutePath basePath)
    {
        var relativePath = System.IO.Path.GetRelativePath(Path, basePath.Path);
        return new RelativePath(relativePath);
    }
    
    public override string ToString() => Path;
}
// -----------------------------------------------------------------------
// <copyright file="SampleFile.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.IO;

namespace Incrementalist.Tests.Helpers;

/// <summary>
/// Sample file data
/// </summary>
/// <param name="Name">Name of the file (might be used by another generated files)</param>
/// <param name="Content">File content</param>
public sealed record SampleFile(string Name, string Content) : IMsBuildSerializable
{
    /// <summary>
    /// Gets full file path
    /// </summary>
    private string GetFullPath(string basePath) => Path.Combine(basePath, Name);

    public AbsolutePath GetFullPath(AbsolutePath basePath) => new AbsolutePath(GetFullPath(basePath.Path));

    public string Serialize()
    {
        return Content;
    }
}
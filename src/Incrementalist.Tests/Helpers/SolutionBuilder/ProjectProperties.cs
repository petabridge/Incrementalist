// -----------------------------------------------------------------------
// <copyright file="ProjectProperties.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace Incrementalist.Tests.Helpers;

public enum PropertyType
{
    Nullable,
    ImplicitUsings,
    OutputType
}

public interface IProjectModelProperty : IMsBuildSerializable
{
    PropertyType PropertyType { get; }
}

public sealed record NullableProperty(bool Enabled) : IProjectModelProperty
{
    public PropertyType PropertyType => PropertyType.Nullable;

    public string Serialize() => $"<Nullable>{Enabled}</Nullable>";
}

public sealed record ImplicitUsingsProperty(bool Enabled) : IProjectModelProperty
{
    public PropertyType PropertyType => PropertyType.ImplicitUsings;

    public string Serialize() => $"<ImplicitUsings>{Enabled}</ImplicitUsings>";
}

public sealed record OutputTypeProperty(OutputType OutputType) : IProjectModelProperty
{
    public PropertyType PropertyType => PropertyType.OutputType;

    public string Serialize() => $"<OutputType>{OutputType}</OutputType>";
}

public sealed record ProjectImport(string RelativePath) : IMsBuildSerializable
{
    public string Serialize() => $"<Import Project=\"{RelativePath}\" />";
}
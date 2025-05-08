// -----------------------------------------------------------------------
// <copyright file="ModuleInitializer.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace Incrementalist.Cmd;

public class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Required for Microsoft.Build.Graph.ProjectGraph to work.
        // Without this, it would fail with "The SDK 'Microsoft.NET.Sdk' specified could not be found."
        MSBuildLocator.RegisterDefaults();
    }
}
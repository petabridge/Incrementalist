// -----------------------------------------------------------------------
// <copyright file="ModuleInitializer.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace Incrementalist.Tests;

public class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Called in Incrementalist.Cmd in the static constructor of the Program class
        // Must also be called for the tests
        MSBuildLocator.RegisterDefaults();
    }
}
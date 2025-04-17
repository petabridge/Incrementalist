// -----------------------------------------------------------------------
// <copyright file="StartupData.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace Incrementalist.Cmd
{
    /// <summary>
    ///     Used for printing startup messages and help.
    /// </summary>
    internal static class StartupData
    {
        public static readonly string VersionNumber =
            FileVersionInfo.GetVersionInfo(typeof(StartupData).Assembly.Location).FileVersion ?? "1.x-unknown";

        public static readonly string ConsoleWindowTitle = $"Incrementalist {VersionNumber}";
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Incrementalist.Console
{
    /// <summary>
    /// Used for printing startup messages and help.
    /// </summary>
    internal static class StartupData
    {
        public static readonly string VersionNumber =
            FileVersionInfo.GetVersionInfo(typeof(StartupData).Assembly.Location).FileVersion;

        public static readonly string ConsoleWindowTitle = $"Incrementalist {VersionNumber}";

        public static void ShowSplash()
        {
            Console.OutputEncoding = Encoding.Unicode;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                @" ____      _        _          _     _            
|  _ \ ___| |_ __ _| |__  _ __(_) __| | __ _  ___ 
| |_) / _ | __/ _` | '_ \| '__| |/ _` |/ _` |/ _ \
|  __|  __| || (_| | |_) | |  | | (_| | (_| |  __/
|_|   \___|\__\__,_|_.__/|_|  |_|\__,_|\__, |\___|
                                       |___/      
");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("petabridge.cmd ({0})", VersionNumber);
            Console.ResetColor();
            Console.WriteLine("Copyright 2017 - {0}, Petabridge®.", DateTime.UtcNow.Year);
            Console.WriteLine();
        }
    }
}

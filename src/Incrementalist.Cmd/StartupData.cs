using System;
using System.Diagnostics;
using System.Text;

namespace Incrementalist.Cmd
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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Incrementalist ({0})", VersionNumber);
            Console.ResetColor();
            Console.WriteLine("Copyright 2015 - {0}, Petabridge®.", DateTime.UtcNow.Year);
            Console.WriteLine();
        }

        public static void ShowHelp()
        {
            Console.WriteLine(
                @"Incrementalist " + VersionNumber + @"

Usage: incrementalist targetBranch [solutionFile]

Options: 
    incrementalist help    Show help

Instructions:

");
        }
    }
}

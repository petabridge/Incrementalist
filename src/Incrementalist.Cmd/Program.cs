using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Incrementalist.Cmd
{
    class Program
    {
        private static string _originalTitle = null;
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static void SetTitle()
        {
            if (IsWindows) // changing console title is not supported on OS X or Linux
            {
                _originalTitle = Console.Title;
                Console.Title = StartupData.ConsoleWindowTitle;
            }
        }

        private static void ResetTitle()
        {
            if (IsWindows)
                Console.Title = _originalTitle; // reset the console window title back
        }

        static int Main(string[] args)
        {
            SetTitle();

            if (args.Length >= 1)
            {
                if (args[0].ToLowerInvariant().Equals("help"))
                {
                    StartupData.ShowHelp();
                    ResetTitle();
                    return 0;
                }
            }

            var cmd = new RepositoryCommands(Directory.GetCurrentDirectory());
            Console.WriteLine("Are we inside repository? {0}", cmd.HaveRepositoryInFolder);

            ResetTitle();
            return 0;
        }
    }
}

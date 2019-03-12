using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Incrementalist.FileSystem;
using Incrementalist.Git;
using LibGit2Sharp;

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

            var insideRepo = Repository.IsValid(Directory.GetCurrentDirectory());
            Console.WriteLine("Are we inside repository? {0}", insideRepo);
            //if (insideRepo)
            //{
            var repoFolder = Repository.Discover(Directory.GetCurrentDirectory());
            var repository = new Repository(repoFolder);
            Console.WriteLine("Repo base is located in {0}", repoFolder);

            foreach(var file in DiffHelper.ChangedFiles(repository, "master"))
                Console.WriteLine("Modified file: {0}", file);
            //}
            if (!string.IsNullOrEmpty(repoFolder))
            {
                var workingFolder = Directory.GetParent(repoFolder).Parent;
                foreach (var sln in SolutionFinder.GetSolutions(workingFolder.FullName))
                {
                    Console.WriteLine("Found solution file {0}", sln);
                }
            }

            ResetTitle();
            return 0;
        }
    }
}

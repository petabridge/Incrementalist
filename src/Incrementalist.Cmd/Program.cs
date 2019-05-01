using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Incrementalist.Cmd.Commands;
using Incrementalist.Git;
using Incrementalist.ProjectSystem;
using LibGit2Sharp;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

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

        static async Task<int> Main(string[] args)
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

            var logger = new ConsoleLogger("Incrementalist", (s, level) => { return level >= LogLevel.Debug; }, false);
            

            var insideRepo = Repository.IsValid(Directory.GetCurrentDirectory());
            Console.WriteLine("Are we inside repository? {0}", insideRepo);
  
            var repoFolder = Repository.Discover(Directory.GetCurrentDirectory());
            var repository = new Repository(repoFolder);
            Console.WriteLine("Repo base is located in {0}", repoFolder);
            var workingFolder = Directory.GetParent(repoFolder).Parent;

            // Locate and register the default instance of MSBuild installed on this machine.
            MSBuildLocator.RegisterDefaults();

            var msBuild = MSBuildWorkspace.Create();
            
            if (!string.IsNullOrEmpty(repoFolder))
            {

                foreach (var sln in SolutionFinder.GetSolutions(workingFolder.FullName))
                {
                    var settings = new BuildSettings("dev", sln, workingFolder.FullName);
                    var emitTask = new EmitDependencyGraphTask(settings, msBuild, logger);
                    var affectedFiles = await emitTask.Run();
                    foreach (var file in affectedFiles)
                    {
                        logger.LogInformation("{0}", file);
                    }
                }
            }

            ResetTitle();
            return 0;
        }
    }
}

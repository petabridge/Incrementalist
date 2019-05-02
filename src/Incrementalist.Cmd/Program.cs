﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommandLine;
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

            SlnOptions options = null;
            var result = Parser.Default.ParseArguments<SlnOptions>(args).MapResult(r =>
            {
                options = r;
                return 0;
            }, _ => 1);

            if (result != 0)
            {
                ResetTitle();
                return result;
            }

            var exitCode = await RunIncrementalist(options);

            ResetTitle();
            return exitCode;
        }

        private static async Task<int> RunIncrementalist(SlnOptions options)
        {
            var logger = new ConsoleLogger("Incrementalist", (s, level) => { return level >= LogLevel.Information; }, false);

            try
            {
                var insideRepo = Repository.IsValid(Directory.GetCurrentDirectory());
                if (!insideRepo)
                {
                    logger.LogError("Current path {0} is not located inside any known Git repository.", Directory.GetCurrentDirectory());
                    return -2;
                }
                var repoFolder = Repository.Discover(Directory.GetCurrentDirectory());
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

                return 0;
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Error encountered during execution of Incrementalist.");
                return -1;
            }
        }
    }
}

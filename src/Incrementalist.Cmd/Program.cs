﻿// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Incrementalist.Cmd
{
    internal class Program
    {
        private static string _originalTitle;
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

        private static async Task<int> Main(string[] args)
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
            // Create a logger factory instance
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information)
                    .AddConsole(loggerOptions => { });
            });

            // Create a logger from the factory
            ILogger logger = loggerFactory.CreateLogger<Program>();

            try
            {
                var pwd = options.WorkingDirectory ?? Directory.GetCurrentDirectory();
                var insideRepo = Repository.IsValid(pwd);
                if (!insideRepo)
                {
                    logger.LogError("Current path {0} is not located inside any known Git repository.", pwd);
                    return -2;
                }


                var repoFolder = Repository.Discover(pwd);
                var workingFolder = Directory.GetParent(repoFolder).Parent;

                var repoResult = GitRunner.FindRepository(workingFolder.FullName);

                if (!repoResult.foundRepo)
                {
                    Console.WriteLine("Unable to find Git repository located in {0}. Shutting down.",
                        workingFolder.FullName);
                    return -3;
                }

                // validate the target branch
                if (!DiffHelper.HasBranch(repoResult.repo, options.GitBranch))
                {
                    // workaround common CI server issues and check to see if this same branch is located
                    // under "origin/{branchname}"
                    options.GitBranch = $"origin/{options.GitBranch}";
                    if (!DiffHelper.HasBranch(repoResult.repo, options.GitBranch))
                    {
                        Console.WriteLine("Current git repository doesn't have any branch named [{0}]. Shutting down.",
                            options.GitBranch);
                        Console.WriteLine("[Debug] Here are all of the currently known branches in this repository");
                        foreach (var b in repoResult.repo.Branches)
                        {
                            Console.WriteLine(b.FriendlyName);
                        }

                        return -4;
                    }
                }

                if (!string.IsNullOrEmpty(repoFolder))
                {
                    if (options.ListFolders)
                        await AnalyzeFolderDiff(options, workingFolder, logger);
                    else
                        await AnaylzeSolutionDIff(options, workingFolder, logger);
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error encountered during execution of Incrementalist.");
                return -1;
            }
        }

        private static async Task AnalyzeFolderDiff(SlnOptions options, DirectoryInfo workingFolder, ILogger logger)
        {
            var settings = new BuildSettings(options.GitBranch, options.SolutionFilePath, workingFolder.FullName,
                TimeSpan.FromMinutes(options.TimeoutMinutes));
            var emitTask = new EmitAffectedFoldersTask(settings, logger);
            var affectedFiles = (await emitTask.Run());

            var affectedFilesStr = string.Join(",", affectedFiles.Keys);

            HandleAffectedFiles(options, affectedFilesStr, affectedFiles.Count);
        }

        private static async Task AnaylzeSolutionDIff(SlnOptions options, DirectoryInfo workingFolder, ILogger logger)
        {
            // Locate and register the default instance of MSBuild installed on this machine.
            MSBuildLocator.RegisterDefaults();

            var msBuild = MSBuildWorkspace.Create();
            if (!string.IsNullOrEmpty(options.SolutionFilePath))
                await ProcessSln(options, options.SolutionFilePath, workingFolder, msBuild, logger);
            else
                foreach (var sln in SolutionFinder.GetSolutions(workingFolder.FullName))
                    await ProcessSln(options, sln, workingFolder, msBuild, logger);
        }

        private static async Task ProcessSln(SlnOptions options, string sln, DirectoryInfo workingFolder,
            MSBuildWorkspace msBuild, ILogger logger)
        {
            var settings = new BuildSettings(options.GitBranch, sln, workingFolder.FullName,
                TimeSpan.FromMinutes(options.TimeoutMinutes));
            var emitTask = new EmitDependencyGraphTask(settings, msBuild, logger);
            var affectedFiles = (await emitTask.Run()).ToList();

            var affectedFilesStr =
                string.Join(Environment.NewLine, affectedFiles.Select(x => string.Join(",", x.Value)));

            HandleAffectedFiles(options, affectedFilesStr, affectedFiles.Count);
        }

        private static void HandleAffectedFiles(SlnOptions options, string affectedFilesStr, int affectedFilesCount)
        {
            if (affectedFilesCount == 0)
            {
                Console.WriteLine("No changes detected by Incrementalist when analyzing {0}.",
                    options.ListFolders ? "repository folders" : "solution");
                return;
            }

            // Check to see if we're planning on writing out to the file system or not.
            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                Console.WriteLine("Detected {0} affected {1} - writing out to {2}", affectedFilesCount,
                    options.ListFolders ? "folders" : "projects in solution", options.OutputFile);
                File.WriteAllText(options.OutputFile, affectedFilesStr);
            }

            else
                Console.WriteLine(affectedFilesStr);
        }
    }
}
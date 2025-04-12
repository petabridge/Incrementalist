// -----------------------------------------------------------------------
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
using Incrementalist.Cmd.Config;
using Incrementalist.Git;
using Incrementalist.ProjectSystem;
using LibGit2Sharp;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Diagnostics;

namespace Incrementalist.Cmd
{
    internal class Program
    {
        private static string _originalTitle = string.Empty;
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static void SetTitle()
        {
            if (IsWindows) // changing console title is not supported on OS X or Linux
            {
#pragma warning disable CA1416
                _originalTitle = Console.Title;
#pragma warning restore CA1416
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

            // Split args at -- to separate incrementalist args from dotnet args
            var splitIndex = Array.IndexOf(args, "--");
            var incrementalistArgs = splitIndex >= 0 ? args.Take(splitIndex).ToArray() : args;
            var dotnetArgs = splitIndex >= 0 ? args.Skip(splitIndex + 1).ToArray() : [];


            SlnOptions? options = null;
            var result = Parser.Default.ParseArguments<SlnOptions>(incrementalistArgs).MapResult(r =>
            {
                options = r;
                options.DotNetArgs = dotnetArgs;
                return 0;
            }, _ => 1);

            if (result != 0)
            {
                ResetTitle();
                return result;
            }

            // Load configuration file if applicable
            IncrementalistConfig? config = null;
            if (IncrementalistConfig.TryLoad(options?.ConfigFile, out var loadedConfig))
            {
                config = loadedConfig;
            }

            // Options has to be populated by the CLI parser
            Debug.Assert(options != null);

            // Merge CLI options with configuration file (CLI takes precedence)
            if (config != null)
            {
                options = ConfigMerger.Merge(options, config);
            }

            // Create a logger factory with the appropriate verbosity
            var minLevel = options.Verbose ? LogLevel.Debug : LogLevel.Information;
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(minLevel);
            });

            // Check if we are creating a configuration file
            if (options.CreateConfig)
            {
                var createConfigTask =
                    new CreateConfigFileTask(options, loggerFactory.CreateLogger<CreateConfigFileTask>());
                var configResult = await createConfigTask.Run();
                ResetTitle();
                return configResult;
            }

            var exitCode = await RunIncrementalist(options, loggerFactory);

            ResetTitle();
            return exitCode;
        }

        private static async Task<int> RunIncrementalist(SlnOptions options, ILoggerFactory loggerFactory)
        {
            // Create a logger from the factory
            ILogger logger = loggerFactory.CreateLogger<Program>();

            try
            {
                var pwd = options.WorkingDirectory ?? Directory.GetCurrentDirectory();
                var insideRepo = Repository.IsValid(pwd);
                if (!insideRepo)
                {
                    logger.LogError("Current path {WorkingDirectory} is not located inside any known Git repository.",
                        pwd);
                    return -2;
                }


                // can't be null or Repository.IsValid(pwd) would have failed
                var repoFolder = Repository.Discover(pwd)!;
                var workingFolder = Directory.GetParent(repoFolder)!.Parent!;

                var (repo, foundRepo) = GitRunner.FindRepository(workingFolder.FullName);

                if (!foundRepo || repo == null)
                {
                    logger.LogError("Unable to find Git repository located in {WorkingDirectory}. Shutting down.",
                        workingFolder.FullName);
                    return -3;
                }

                // validate the target branch
                if (!DiffHelper.HasBranch(repo, options.GitBranch!))
                {
                    // workaround common CI server issues and check to see if this same branch is located
                    // under "origin/{branchname}"
                    options.GitBranch = $"origin/{options.GitBranch}";
                    if (!DiffHelper.HasBranch(repo, options.GitBranch))
                    {
                        logger.LogError(
                            "Current git repository doesn't have any branch named [{Branch}]. Shutting down.",
                            options.GitBranch);
                        logger.LogInformation("Here are all of the currently known branches in this repository:");
                        foreach (var b in repo.Branches)
                        {
                            logger.LogInformation(b.FriendlyName);
                        }

                        return -4;
                    }
                }

                if (!string.IsNullOrEmpty(repoFolder))
                {
                    if (options.ListFolders)
                        await AnalyzeFolderDiff(options, workingFolder, logger);
                    else
                        await AnalyzeSolutionDIff(options, workingFolder, logger);
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
            /*
             * options.SolutionFilePath can be null here, but it won't affect this task
             */

            var settings = new BuildSettings(options.GitBranch!, options.SolutionFilePath ?? string.Empty,
                workingFolder.FullName,
                TimeSpan.FromMinutes(options.TimeoutMinutes))
            {
                NoCache = options.NoCache
            };
            var emitTask = new EmitAffectedFoldersTask(settings, logger);
            var affectedFiles = (await emitTask.Run());

            var affectedFilesStr = string.Join(",", affectedFiles.Keys);

            HandleAffectedFiles(options, affectedFilesStr, affectedFiles.Count, logger);
        }

        private static async Task AnalyzeSolutionDIff(SlnOptions options, DirectoryInfo workingFolder, ILogger logger)
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
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            logger.LogInformation("Starting analysis of solution: {Solution}", sln);

            var settings = new BuildSettings(options.GitBranch!, sln, workingFolder.FullName,
                TimeSpan.FromMinutes(options.TimeoutMinutes))
            {
                NoCache = options.NoCache
            };

            logger.LogInformation("Beginning dependency analysis...");
            var emitTask = new EmitDependencyGraphTask(settings, msBuild, logger);
            var buildResult = await emitTask.Run();

            var analysisTime = stopwatch.Elapsed;
            logger.LogInformation("Solution analysis completed in {Duration:g}", analysisTime);

            if (options is { RunCommand: true, DotNetArgs.Length: > 0 })
            {
                var runTask = new RunDotNetCommandTask(settings, logger, options.DotNetArgs,
                    options.ContinueOnError, options.RunInParallel, options.FailOnNoProjects);

                var exitCode = await runTask.Run(buildResult);
                if (exitCode != 0)
                    throw new Exception($"Command execution failed with exit code {exitCode}");
            }
            else
            {
                string buildType;
                IReadOnlyList<string> projectsToRebuild;

                switch (buildResult)
                {
                    case FullSolutionBuildResult _:
                        buildType = "Full solution build";
                        projectsToRebuild = msBuild.CurrentSolution.Projects.Where(p => p.FilePath is not null)
                            .Select(p => p.FilePath!).ToList();
                        break;
                    case IncrementalBuildResult incremental:
                        buildType = "Incremental build";
                        projectsToRebuild = incremental.AffectedProjects;
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown build result type: {buildResult.GetType()}");
                }

                if (!projectsToRebuild.Any())
                {
                    logger.LogInformation("No changes detected by Incrementalist when analyzing solution");
                    return;
                }

                var affectedFilesStr = string.Join(Environment.NewLine, projectsToRebuild);

                // Check to see if we're planning on writing out to the file system or not.
                if (!string.IsNullOrEmpty(options.OutputFile))
                {
                    logger.LogInformation("{BuildType} required - {AffectedProjects} affected projects - writing out to {OutputFilePath}",
                        buildType,
                        projectsToRebuild.Count,
                        options.OutputFile);
                    await File.WriteAllTextAsync(options.OutputFile, affectedFilesStr);
                }
                else
                {
                    logger.LogInformation("{BuildType} required:", buildType);
                    logger.LogInformation(affectedFilesStr);
                }
            }
        }

        private static void HandleAffectedFiles(SlnOptions options, string affectedFilesStr, int affectedFilesCount,
            ILogger logger)
        {
            if (affectedFilesCount == 0)
            {
                logger.LogInformation("No changes detected by Incrementalist when analyzing {0}.",
                    options.ListFolders ? "repository folders" : "solution");
                return;
            }

            // Check to see if we're planning on writing out to the file system or not.
            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                logger.LogInformation("Detected {0} affected {1} - writing out to {2}", affectedFilesCount,
                    options.ListFolders ? "folders" : "projects in solution", options.OutputFile);
                File.WriteAllText(options.OutputFile, affectedFilesStr);
            }
            else
                logger.LogInformation(affectedFilesStr);
        }
    }
}
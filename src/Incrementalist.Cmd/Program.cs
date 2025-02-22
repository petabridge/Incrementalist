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
        private static string _originalTitle = string.Empty;
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
            ArgumentNullException.ThrowIfNull(args);
            SetTitle();

            // Split args at -- to separate incrementalist args from dotnet args
            var splitIndex = Array.IndexOf(args, "--");
            var incrementalistArgs = splitIndex >= 0 ? args.Take(splitIndex).ToArray() : args;
            var dotnetArgs = splitIndex >= 0 ? args.Skip(splitIndex + 1).ToArray() : Array.Empty<string>();

            SlnOptions? options = null;
            var result = Parser.Default.ParseArguments<SlnOptions>(incrementalistArgs).MapResult(r =>
            {
                options = r;
                options.DotNetArgs = dotnetArgs;
                return 0;
            }, _ => 1);

            if (result != 0 || options == null)
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
            ArgumentNullException.ThrowIfNull(options);

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
                if (string.IsNullOrEmpty(repoFolder))
                {
                    logger.LogError("Unable to discover Git repository in {0}. Shutting down.", pwd);
                    return -3;
                }

                var workingFolder = Directory.GetParent(repoFolder);
                if (workingFolder?.Parent == null)
                {
                    logger.LogError("Unable to determine working folder from repository path {0}. Shutting down.", repoFolder);
                    return -3;
                }

                var repoResult = GitRunner.FindRepository(workingFolder.Parent.FullName);

                if (!repoResult.foundRepo || repoResult.repo == null)
                {
                    logger.LogError("Unable to find Git repository located in {0}. Shutting down.", workingFolder.Parent.FullName);
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
                        logger.LogError("Current git repository doesn't have any branch named [{0}]. Shutting down.", options.GitBranch);
                        logger.LogDebug("Here are all of the currently known branches in this repository:");
                        foreach (var b in repoResult.repo.Branches)
                        {
                            logger.LogDebug(b.FriendlyName);
                        }

                        return -4;
                    }
                }

                await (options.ListFolders
                    ? AnalyzeFolderDiff(options, workingFolder.Parent, logger)
                    : AnaylzeSolutionDIff(options, workingFolder.Parent, logger));

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
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(workingFolder);
            ArgumentNullException.ThrowIfNull(logger);

            var settings = new BuildSettings(options.GitBranch, options.SolutionFilePath, workingFolder.FullName,
                TimeSpan.FromMinutes(options.TimeoutMinutes));
            var emitTask = new EmitAffectedFoldersTask(settings, logger);
            var affectedFiles = await emitTask.Run();

            var affectedFilesStr = string.Join(",", affectedFiles.Keys);

            HandleAffectedFiles(options, affectedFilesStr, affectedFiles.Count, logger);
        }

        private static async Task AnaylzeSolutionDIff(SlnOptions options, DirectoryInfo workingFolder, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(workingFolder);
            ArgumentNullException.ThrowIfNull(logger);

            // Locate and register the default instance of MSBuild installed on this machine.
            MSBuildLocator.RegisterDefaults();

            var msBuild = MSBuildWorkspace.Create();
            if (!string.IsNullOrEmpty(options.SolutionFilePath))
                await ProcessSln(options, options.SolutionFilePath, workingFolder, msBuild, logger);
            else
            {
                var solutions = SolutionFinder.GetSolutions(workingFolder.FullName).ToList();
                if (solutions.Count == 0)
                {
                    logger.LogWarning("No solution files found in {0}", workingFolder.FullName);
                    return;
                }
                foreach (var sln in solutions)
                    await ProcessSln(options, sln, workingFolder, msBuild, logger);
            }
        }

        private static async Task ProcessSln(SlnOptions options, string sln, DirectoryInfo workingFolder,
            MSBuildWorkspace msBuild, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(sln);
            ArgumentNullException.ThrowIfNull(workingFolder);
            ArgumentNullException.ThrowIfNull(msBuild);
            ArgumentNullException.ThrowIfNull(logger);

            var settings = new BuildSettings(options.GitBranch, sln, workingFolder.FullName,
                TimeSpan.FromMinutes(options.TimeoutMinutes));
            var emitTask = new EmitDependencyGraphTask(settings, msBuild, logger);
            var buildResult = await emitTask.Run();

            if (options.RunCommand && options.DotNetArgs.Length > 0)
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
                IEnumerable<string> projectsToRebuild;

                switch (buildResult)
                {
                    case FullSolutionBuildResult _:
                        buildType = "Full solution build";
                        projectsToRebuild = msBuild.CurrentSolution.Projects
                            .Select(p => p.FilePath)
                            .Where(p => !string.IsNullOrEmpty(p))
                            .ToList()!;
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
                    logger.LogInformation("{0} required - {1} affected projects - writing out to {2}", 
                        buildType,
                        projectsToRebuild.Count(),
                        options.OutputFile);
                    File.WriteAllText(options.OutputFile, affectedFilesStr);
                }
                else
                {
                    logger.LogInformation("{0} required:", buildType);
                    logger.LogInformation(affectedFilesStr);
                }
            }
        }

        private static void HandleAffectedFiles(SlnOptions options, string affectedFilesStr, int affectedFilesCount, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(affectedFilesStr);
            ArgumentNullException.ThrowIfNull(logger);

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
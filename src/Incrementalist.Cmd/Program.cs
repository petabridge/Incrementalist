// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Incrementalist.Cmd.Commands;
using Incrementalist.Cmd.Config;
using Incrementalist.Git;
using Incrementalist.ProjectSystem;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Locator;
using static Incrementalist.Cmd.SlnOptionsParser;

namespace Incrementalist.Cmd
{
    internal class Program
    {
        private static string _originalTitle = string.Empty;
        private static CancellationTokenSource _processCts = new CancellationTokenSource();
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        static Program()
        {
            // Required for Microsoft.Build.Graph.ProjectGraph to work.
            // Without this, it would fail with "The SDK 'Microsoft.NET.Sdk' specified could not be found."
            MSBuildLocator.RegisterDefaults();
        }

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

            var result = TryParseSlnOptions(args, out var cmdConfiguration);

            if (result != 0)
            {
                ResetTitle();
                return result;
            }

            // Load configuration file if applicable
            IncrementalistConfig? config = null;
            bool haveConfig = false;
            if (IncrementalistConfig.TryLoad(cmdConfiguration?.ConfigFile, out var loadedConfig))
            {
                haveConfig = true;
                config = loadedConfig;
            }

            // Options has to be populated by the CLI parser
            Debug.Assert(cmdConfiguration != null);

            // Merge CLI options with configuration file (CLI takes precedence)
            if (config != null)
            {
                cmdConfiguration = ConfigMerger.Merge(cmdConfiguration, config);
            }

            // Create a logger factory with the appropriate verbosity
            var minLevel = cmdConfiguration.Verbose ? LogLevel.Debug : LogLevel.Information;
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(minLevel);
            });

            // Check if we are creating a configuration file
            if (cmdConfiguration is CreateConfigOptions createConfigOptions)
            {
                var createConfigTask =
                    new CreateConfigFileTask(createConfigOptions, loggerFactory.CreateLogger<CreateConfigFileTask>());
                var configResult = await createConfigTask.Run();
                ResetTitle();
                return configResult;
            }
            
            // Create a logger from the factory
            ILogger logger = loggerFactory.CreateLogger<Program>();
            
            Console.CancelKeyPress += delegate {
                // call methods to clean up
                logger.LogWarning("Cancellation requested. Exiting...");
                _processCts.Cancel();
            };
            
            var exitCode = await RunIncrementalist(cmdConfiguration, logger, haveConfig, _processCts.Token);

            ResetTitle();
            return exitCode;
        }

        private static async Task<int> RunIncrementalist(SlnOptions cmdOptions, ILogger logger,
            bool loadedConfig, CancellationToken ct)
        {
            try
            {
                if (loadedConfig)
                {
                    var configFileName = cmdOptions.ConfigFile ?? Path.Combine(IncrementalistConfig.IncrementalistDirectory, IncrementalistConfig.DefaultConfigFileName);
                    logger.LogInformation("Loaded configuration file: {ConfigFile}", configFileName);
                }

                var pwd = new AbsolutePath(
                    Path.GetFullPath(cmdOptions.WorkingDirectory ?? Directory.GetCurrentDirectory()));

                var insideRepo = Repository.IsValid(pwd.Path);
                if (!insideRepo)
                {
                    logger.LogError("Current path {WorkingDirectory} is not located inside any known Git repository.",
                        pwd);
                    return -2;
                }

                // can't be null or Repository.IsValid(pwd) would have failed
                var repoFolder = Repository.Discover(pwd.Path)!;
                var workingFolder = new AbsolutePath(Directory.GetParent(repoFolder)!.Parent!.FullName);

                var (repo, foundRepo) = GitRunner.FindRepository(workingFolder);

                if (!foundRepo || repo == null)
                {
                    logger.LogError("Unable to find Git repository located in {WorkingDirectory}. Shutting down.",
                        workingFolder);
                    return -3;
                }

                // validate the target branch
                if (!DiffHelper.HasBranch(repo, cmdOptions.GitBranch!))
                {
                    // workaround common CI server issues and check to see if this same branch is located
                    // under "origin/{branchname}"
                    cmdOptions.GitBranch = $"origin/{cmdOptions.GitBranch}";
                    if (!DiffHelper.HasBranch(repo, cmdOptions.GitBranch))
                    {
                        logger.LogError(
                            "Current git repository doesn't have any branch named [{Branch}]. Shutting down.",
                            cmdOptions.GitBranch);
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
                    switch (cmdOptions)
                    {
                        case ListFoldersOptions listFoldersOptions:
                            await AnalyzeFolderDiff(listFoldersOptions, workingFolder, logger, ct);
                            break;
                        case RunOptions runOptions:
                            await AnalyzeSolutionDIff(runOptions, workingFolder, logger, ct);
                            break;
                        case RunProcessOptions runProcessOptions:
                            await AnalyzeSolutionDiffProcess(runProcessOptions, workingFolder, logger, ct);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(cmdOptions),
                                $"Unknown command line option type: {cmdOptions.GetType()}");
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error encountered during execution of Incrementalist.");
                return -1;
            }
        }

        private static async Task AnalyzeFolderDiff(ListFoldersOptions options, AbsolutePath workingFolder,
            ILogger logger, CancellationToken ct)
        {
            /*
             * options.SolutionFilePath can be null here, but it won't affect this task
             */

            var normalized = options.SolutionFilePath != null
                ? workingFolder.ComputeRelativePathToMe(new AbsolutePath(Path.GetFullPath(options.SolutionFilePath)))
                : RelativePath.Empty;

            var settings = new BuildSettings(options.GitBranch!, normalized,
                workingFolder,
                options.SkipGlobs?.ToArray() ?? [],
                options.TargetGlobs?.ToArray() ?? [],
                options.NameApplicationToStart,
                TimeSpan.FromMinutes(options.TimeoutMinutes));
            var emitTask = new EmitAffectedFoldersTask(settings, logger, ct);
            var affectedFiles = (await emitTask.Run());

            var affectedFilesStr = string.Join(",", affectedFiles.Keys);

            await HandleAffectedFiles(options, affectedFilesStr, affectedFiles.Count, logger);
        }

        private static BuildEngine CreateBuildEngine(SlnOptions options, ILogger logger)
        {
            var engine = Engine.StaticGraph;
            var verbose = false;
            switch (options)
            {
                case RunOptions runOptions:
                    engine = runOptions.Engine;
                    verbose = runOptions.Verbose;
                    break;
                case RunProcessOptions runProcessOptions:
                    engine = runProcessOptions.Engine;
                    verbose = runProcessOptions.Verbose;
                    break;
            }
            return engine switch
            {
                Engine.StaticGraph => new StaticGraphBuildEngine(logger, verbose),
                Engine.Workspace => new WorkspaceBuildEngine(logger),
                _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, null)
            };
        }

        private static async Task AnalyzeSolutionDIff(RunOptions options, AbsolutePath workingFolder, ILogger logger, CancellationToken ct)
        {
            using var engine = CreateBuildEngine(options, logger);

            if (!string.IsNullOrEmpty(options.SolutionFilePath))
            {
                var normalizedPath =
                    workingFolder.ComputeRelativePathToMe(new AbsolutePath(Path.GetFullPath(options.SolutionFilePath)));

                await ProcessSln(options, normalizedPath, workingFolder, engine, logger, ct);
            }

            else
                foreach (var sln in SolutionFinder.GetSolutions(workingFolder))
                    await ProcessSln(options, sln, workingFolder, engine, logger, ct);
        }

        private static async Task ProcessSln(RunOptions options, RelativePath sln, AbsolutePath workingFolder,
            BuildEngine engine, ILogger logger, CancellationToken ct)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            logger.LogInformation("Starting analysis of solution: {Solution}", sln);

            var settings = new BuildSettings(options.GitBranch!, sln, workingFolder,
                options.SkipGlobs?.ToArray() ?? [],
                options.TargetGlobs?.ToArray() ?? [],
                options.NameApplicationToStart,
                TimeSpan.FromMinutes(options.TimeoutMinutes));

            logger.LogInformation("Beginning dependency analysis...");
            var emitTask = new EmitDependencyGraphTask(settings, engine, logger, ct);
            var buildResult = await emitTask.Run();

            var analysisTime = stopwatch.Elapsed;
            logger.LogInformation("Solution analysis completed in {Duration:g}", analysisTime);

            if (options is { DryRun: false, DotNetArgs.Length: > 0 })
            {
                var runTask = new RunDotNetCommandTask(settings, logger, options.DotNetArgs,
                    options.ContinueOnError, options.RunInParallel, options.ParallelLimit, ct, options.FailOnNoProjects);

                var exitCode = await runTask.Run(buildResult);
                if (exitCode != 0)
                    throw new Exception($"Command execution failed with exit code {exitCode}");
            }
            else
            {
                string buildType;
                IReadOnlyList<AbsolutePath> projectsToRebuild;

                switch (buildResult)
                {
                    case FullSolutionBuildResult:
                        buildType = "Full solution build";
                        var solutionFilePath = new AbsolutePath(Path.Join(settings.WorkingDirectory.Path, settings.SolutionFile.Path));
                        projectsToRebuild = (await engine.CreateSolutionAsync(solutionFilePath, ct)).Projects.Select(p => p.FilePath).ToList();
                        break;
                    case IncrementalBuildResult incremental:
                        buildType = "Incremental build";
                        projectsToRebuild = incremental.AffectedProjects;
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown build result type: {buildResult.GetType()}");
                }

                var affectedFilesStr = string.Join(Environment.NewLine, projectsToRebuild);

                if (!projectsToRebuild.Any())
                {
                    logger.LogInformation("No changes detected by Incrementalist when analyzing solution");

                    // log an empty file anyway
                    await WriteOutputFileAsync(options, logger, affectedFilesStr);
                    return;
                }

                logger.LogInformation("Calculating dry run....");

                logger.LogInformation("{BuildType} required:", buildType);
                logger.LogInformation("{AffectedProjects} affected projects: {AllProjectList}",
                    projectsToRebuild.Count, affectedFilesStr);

                // Check to see if we're planning on writing out to the file system or not.
                if (!string.IsNullOrEmpty(options.OutputFile))
                {
                    await WriteOutputFileAsync(options, logger, affectedFilesStr);
                }
            }
        }

        private static async Task WriteOutputFileAsync(SlnOptions options, ILogger logger, string affectedFilesStr)
        {
            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                // ensure directory, per https://github.com/petabridge/Incrementalist/issues/377
                var path = Path.GetDirectoryName(options.OutputFile);
                if (path != null && !Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                
                logger.LogInformation("Writing to output file: {OutputFile}", options.OutputFile);
               
                await File.WriteAllTextAsync(options.OutputFile, affectedFilesStr);
            }
        }

        private static async Task HandleAffectedFiles(SlnOptions options, string affectedFilesStr, int affectedFilesCount,
            ILogger logger)
        {
            var listingFolders = options is ListFoldersOptions;

            if (affectedFilesCount == 0)
            {
                logger.LogInformation("No changes detected by Incrementalist when analyzing {FileSysType}.",
                    listingFolders ? "repository folders" : "solution");
                return;
            }

            // Check to see if we're planning on writing out to the file system or not.
            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                logger.LogInformation("Detected {AffectedFiles} affected {FileSysType} - writing out to {OutputFile}",
                    affectedFilesCount,
                    listingFolders ? "folders" : "projects in solution", options.OutputFile);
                
                await WriteOutputFileAsync(options, logger, affectedFilesStr);
            }
            else
                logger.LogInformation(affectedFilesStr);
        }

        // For now, run-process is a discoverable alias for run, but only dotnet commands are supported.
        private static async Task AnalyzeSolutionDiffProcess(RunProcessOptions options, AbsolutePath workingFolder, ILogger logger, CancellationToken ct)
        {
            logger.LogWarning("[run-process] Only dotnet commands are currently supported. This is a discoverability alias for 'run'.");
            // Forward to AnalyzeSolutionDIff, mapping ProcessName/ProcessArgs to DotNetArgs
            var runOptions = new RunOptions
            {
                SolutionFilePath = options.SolutionFilePath,
                OutputFile = options.OutputFile,
                GitBranch = options.GitBranch,
                WorkingDirectory = options.WorkingDirectory,
                Verbose = options.Verbose,
                TimeoutMinutes = options.TimeoutMinutes,
                ConfigFile = options.ConfigFile,
                ContinueOnError = options.ContinueOnError,
                RunInParallel = options.RunInParallel,
                FailOnNoProjects = options.FailOnNoProjects,
                Engine = options.Engine,
                DotNetArgs = new[] { options.ProcessName }.Concat(options.ProcessArgs ?? Array.Empty<string>()).ToArray(),
            };
            await AnalyzeSolutionDIff(runOptions, workingFolder, logger, ct);
        }
    }
}

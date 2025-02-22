// -----------------------------------------------------------------------
// <copyright file="RunDotNetCommandTask.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2023 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd.Commands
{
    /// <summary>
    /// Task that executes dotnet CLI commands against affected projects
    /// </summary>
    public class RunDotNetCommandTask
    {
        private readonly BuildSettings _settings;
        private readonly ILogger _logger;
        private readonly string[] _dotnetArgs;
        private readonly bool _continueOnError;
        private readonly bool _runInParallel;
        private readonly bool _failOnNoProjects;

        public RunDotNetCommandTask(BuildSettings settings, ILogger logger, string[] dotnetArgs, bool continueOnError, bool runInParallel, bool failOnNoProjects = false)
        {
            _settings = settings;
            _logger = logger;
            _dotnetArgs = dotnetArgs;
            _continueOnError = continueOnError;
            _runInParallel = runInParallel;
            _failOnNoProjects = failOnNoProjects;
        }

        public async Task<int> Run(BuildAnalysisResult buildResult)
        {
            switch (buildResult)
            {
                case FullSolutionBuildResult full:
                    return await RunSolutionBuild(full.SolutionPath);
                case IncrementalBuildResult incremental:
                    return await RunIncrementalBuild(incremental.AffectedProjects);
                default:
                    throw new InvalidOperationException($"Unknown build result type: {buildResult.GetType()}");
            }
        }

        private async Task<int> RunSolutionBuild(string solutionPath)
        {
            _logger.LogInformation("Running '{0}' against solution {1}", string.Join(" ", _dotnetArgs), solutionPath);
            return await RunCommand(solutionPath);
        }

        private async Task<int> RunIncrementalBuild(IEnumerable<string> affectedProjects)
        {
            var projects = affectedProjects.ToList();
            if (!projects.Any())
            {
                _logger.LogInformation("No affected projects to run commands against.");
                return _failOnNoProjects ? 1 : 0;
            }

            _logger.LogInformation("Running '{0}' against {1} affected projects", string.Join(" ", _dotnetArgs), projects.Count);
            
            var failedProjects = new List<string>();
            
            if (_runInParallel)
            {
                var tasks = projects.Select(async project =>
                {
                    if (await RunCommand(project) != 0)
                    {
                        failedProjects.Add(project);
                        if (!_continueOnError)
                            return;
                    }
                });
                
                await Task.WhenAll(tasks);
            }
            else
            {
                foreach (var project in projects)
                {
                    if (await RunCommand(project) != 0)
                    {
                        failedProjects.Add(project);
                        if (!_continueOnError)
                            break;
                    }
                }
            }

            if (failedProjects.Count != 0)
            {
                _logger.LogError("Command failed for the following projects: {0}", string.Join(", ", failedProjects));
                return 1;
            }

            return 0;
        }

        private async Task<int> RunCommand(string target)
        {
            // For dotnet CLI commands like 'build', 'test', etc., the project/solution path comes last
            var args = string.Join(" ", _dotnetArgs);
            if (!args.Contains("--project") && !args.Contains("-p"))
                args = $"{args} \"{target}\"";

            _logger.LogInformation("Executing 'dotnet {0}' for {1}", args, target);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _settings.WorkingDirectory
                }
            };

            // Redirect to console streams directly
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.Out.WriteLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.Error.WriteLine(e.Data);
            };

            try
            {                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Command failed for {0} with exit code {1}", target, process.ExitCode);
                }
                
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute command for {0}", target);
                return 1;
            }
            finally
            {
                process.Dispose();
            }
        }
    }
} 
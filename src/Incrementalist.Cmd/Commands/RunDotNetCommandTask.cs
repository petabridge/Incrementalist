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
using Incrementalist.ProjectSystem;

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
        private readonly DotNetCommandOptimizer _optimizer;

        public RunDotNetCommandTask(BuildSettings settings, ILogger logger, string[] dotnetArgs, bool continueOnError, bool runInParallel, bool failOnNoProjects = false)
        {
            _settings = settings;
            _logger = logger;
            _dotnetArgs = dotnetArgs;
            _continueOnError = continueOnError;
            _runInParallel = runInParallel;
            _failOnNoProjects = failOnNoProjects;
            _optimizer = new DotNetCommandOptimizer();
        }

        public async Task<int> Run(IEnumerable<string> affectedProjects)
        {
            var projects = affectedProjects.ToList();
            if (!projects.Any())
            {
                _logger.LogInformation("No affected projects to run commands against.");
                return _failOnNoProjects ? 1 : 0;
            }

            var command = string.Join(" ", _dotnetArgs);
            _logger.LogInformation("Running '{0}' against {1} affected projects", command, projects.Count);
            
            var failedProjects = new List<string>();

            // Check if we need to run on full solution
            if (_optimizer.ShouldRunOnFullSolution(projects))
            {
                _logger.LogInformation("Solution-level files detected. Running command on entire solution.");
                var result = await RunCommand(_settings.SolutionFile);
                return result ? 0 : 1;
            }

            // Get optimized commands
            var commands = _optimizer.OptimizeCommand(command, projects, _settings.SolutionFile);

            if (_runInParallel)
            {
                var tasks = commands.Select(async cmd =>
                {
                    if (!await RunCommand(cmd))
                    {
                        failedProjects.Add(cmd);
                        if (!_continueOnError)
                            return;
                    }
                });
                
                await Task.WhenAll(tasks);
            }
            else
            {
                foreach (var cmd in commands)
                {
                    if (!await RunCommand(cmd))
                    {
                        failedProjects.Add(cmd);
                        if (!_continueOnError)
                            break;
                    }
                }
            }

            if (failedProjects.Count != 0)
            {
                _logger.LogError("Command failed for the following commands: {0}", string.Join(", ", failedProjects));
                return 1;
            }

            return 0;
        }

        private async Task<bool> RunCommand(string args)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = _settings.WorkingDirectory
                }
            };

            process.OutputDataReceived += (sender, eventArgs) =>
            {
                if (!string.IsNullOrEmpty(eventArgs.Data))
                    _logger.LogInformation("[{0}] {1}", args, eventArgs.Data);
            };

            process.ErrorDataReceived += (sender, eventArgs) =>
            {
                if (!string.IsNullOrEmpty(eventArgs.Data))
                    _logger.LogError("[{0}] {1}", args, eventArgs.Data);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Command failed with exit code {0}: {1}", process.ExitCode, args);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute command: {0}", args);
                return false;
            }
            finally
            {
                process.Dispose();
            }
        }
    }
} 
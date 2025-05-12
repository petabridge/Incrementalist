// -----------------------------------------------------------------------
// <copyright file="RunDotNetCommandTask.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
        private readonly CancellationToken _ct;

        public RunDotNetCommandTask(BuildSettings settings, ILogger logger, string[] dotnetArgs, bool continueOnError,
            bool runInParallel, CancellationToken ct, bool failOnNoProjects = false)
        {
            _settings = settings;
            _logger = logger;
            _dotnetArgs = dotnetArgs;
            _continueOnError = continueOnError;
            _runInParallel = runInParallel;
            _ct = ct;
            _failOnNoProjects = failOnNoProjects;
        }

        public async Task<int> Run(BuildAnalysisResult buildResult)
        {
            switch (buildResult)
            {
                case FullSolutionBuildResult full:
                    return await RunSolutionBuild(full.SolutionPath, _ct);
                case IncrementalBuildResult incremental:
                    return await RunIncrementalBuild(incremental.AffectedProjects, _ct);
                default:
                    throw new InvalidOperationException($"Unknown build result type: {buildResult.GetType()}");
            }
        }

        private async Task<int> RunSolutionBuild(AbsolutePath solutionPath, CancellationToken ct)
        {
            _logger.LogInformation("Running '{CommandString}' against solution {Solution}", string.Join(" ", _dotnetArgs), solutionPath);
            return await RunCommandAsync(solutionPath, ct);
        }

        private async Task<int> RunIncrementalBuild(IEnumerable<AbsolutePath> affectedProjects, CancellationToken ct)
        {
            var projects = affectedProjects.ToList();
            if (projects.Count == 0)
            {
                _logger.LogInformation("No affected projects to run commands against.");
                return _failOnNoProjects ? 1 : 0;
            }

            _logger.LogInformation("Running '{CommandString}' against {Projects} affected projects", string.Join(" ", _dotnetArgs),
                projects.Count);

            var failedProjects = new List<AbsolutePath>();

            if (_runInParallel)
            {
                var tasks = projects.Select(async project =>
                {
                    if (await RunCommandAsync(project, ct) != 0)
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
                    if (await RunCommandAsync(project, ct) != 0)
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

        private async Task<int> RunCommandAsync(AbsolutePath target, CancellationToken ct)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(_settings.TimeoutDuration);
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _settings.WorkingDirectory.Path
                }
            };

            // Add all dotnet arguments
            foreach (var arg in _dotnetArgs)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            // Add target project/solution if not already specified
            if (!_dotnetArgs.Any(x => x is "--project" or "-p"))
            {
                process.StartInfo.ArgumentList.Add(target.Path);
            }

            _logger.LogInformation("Executing 'dotnet {ArgsList}' for {Target}",
                string.Join(" ", process.StartInfo.ArgumentList), target);

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
            
            var startTime = DateTime.UtcNow;

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(linkedCts.Token);

                var finishTime = DateTime.UtcNow;
                var elapsedTime = finishTime - startTime;
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Command 'dotnet {ArgsList}' failed for {Target} with exit code {ExitCode} after {ElapsedTime}",
                        string.Join(" ", process.StartInfo.ArgumentList), target, process.ExitCode, elapsedTime);
                }
                else
                {
                    // successful exit
                    _logger.LogDebug("Command 'dotnet {ArgsList}' succeeded for {Target} after {ElapsedTime}",
                        string.Join(" ", process.StartInfo.ArgumentList), target, elapsedTime);
                }

                return process.ExitCode;
            }
            catch (Exception ex)
            {
                var finishTime = DateTime.UtcNow;
                var elapsedTime = finishTime - startTime;
                if (ex is TaskCanceledException or OperationCanceledException)
                {
                    _logger.LogWarning("Command 'dotnet {ArgsList}' was canceled for {Target} after {ElapsedTime}",
                        string.Join(" ", process.StartInfo.ArgumentList), target, elapsedTime);
                }
                else
                {
                    _logger.LogError(ex, "Failed to execute command 'dotnet {ArgsList}' for {Target} after {ElapsedTime}",
                        string.Join(" ", process.StartInfo.ArgumentList), target, elapsedTime);
                    
                }
                return 1;
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// Task that executes arbitrary process commands against affected projects
    /// </summary>
    public class RunProcessCommandTask
    {
        private readonly BuildSettings _settings;
        private readonly ILogger _logger;
        private readonly string _processName;
        private readonly string[] _processArgs;
        private readonly bool _continueOnError;
        private readonly bool _runInParallel;
        private readonly bool _failOnNoProjects;
        private readonly CancellationToken _ct;

        public RunProcessCommandTask(BuildSettings settings, ILogger logger, string processName, string[] processArgs, bool continueOnError,
            bool runInParallel, CancellationToken ct, bool failOnNoProjects = false)
        {
            _settings = settings;
            _logger = logger;
            _processName = processName;
            _processArgs = processArgs;
            _continueOnError = continueOnError;
            _runInParallel = runInParallel;
            _ct = ct;
            _failOnNoProjects = failOnNoProjects;
        }

        public async Task<int> Run(BuildAnalysisResult buildResult)
        {
            switch (buildResult)
            {
                case FullSolutionBuildResult full:
                    return await RunSolutionBuild(full.SolutionPath, _ct);
                case IncrementalBuildResult incremental:
                    return await RunIncrementalBuild(incremental.AffectedProjects, _ct);
                default:
                    throw new InvalidOperationException($"Unknown build result type: {buildResult.GetType()}");
            }
        }

        private async Task<int> RunSolutionBuild(AbsolutePath solutionPath, CancellationToken ct)
        {
            _logger.LogInformation("Running '{ProcessName} {CommandString}' against solution {Solution}", _processName, string.Join(" ", _processArgs), solutionPath);
            return await RunCommandAsync(solutionPath, ct);
        }

        private async Task<int> RunIncrementalBuild(IEnumerable<AbsolutePath> affectedProjects, CancellationToken ct)
        {
            var projects = affectedProjects.ToList();
            if (projects.Count == 0)
            {
                _logger.LogInformation("No affected projects to run commands against.");
                return _failOnNoProjects ? 1 : 0;
            }

            _logger.LogInformation("Running '{ProcessName} {CommandString}' against {Projects} affected projects", _processName, string.Join(" ", _processArgs), projects.Count);

            var failedProjects = new List<AbsolutePath>();

            if (_runInParallel)
            {
                var tasks = projects.Select(async project =>
                {
                    if (await RunCommandAsync(project, ct) != 0)
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
                    if (await RunCommandAsync(project, ct) != 0)
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

        private async Task<int> RunCommandAsync(AbsolutePath target, CancellationToken ct)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(_settings.TimeoutDuration);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _processName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _settings.WorkingDirectory.Path
                }
            };

            // Add all process arguments
            foreach (var arg in _processArgs)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            // Add target project/solution if not already specified
            if (!_processArgs.Any(x => x is "--project" or "-p"))
            {
                process.StartInfo.ArgumentList.Add(target.Path);
            }

            _logger.LogInformation("Executing '{ProcessName} {ArgsList}' for {Target}", _processName, string.Join(" ", process.StartInfo.ArgumentList), target);

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

            var startTime = DateTime.UtcNow;

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(linkedCts.Token);

                var finishTime = DateTime.UtcNow;
                var elapsedTime = finishTime - startTime;

                if (process.ExitCode != 0)
                {
                    _logger.LogError("Command '{ProcessName} {ArgsList}' failed for {Target} with exit code {ExitCode} after {ElapsedTime}",
                        _processName, string.Join(" ", process.StartInfo.ArgumentList), target, process.ExitCode, elapsedTime);
                }
                else
                {
                    // successful exit
                    _logger.LogDebug("Command '{ProcessName} {ArgsList}' succeeded for {Target} after {ElapsedTime}",
                        _processName, string.Join(" ", process.StartInfo.ArgumentList), target, elapsedTime);
                }

                return process.ExitCode;
            }
            catch (Exception ex)
            {
                var finishTime = DateTime.UtcNow;
                var elapsedTime = finishTime - startTime;
                if (ex is TaskCanceledException or OperationCanceledException)
                {
                    _logger.LogWarning("Command '{ProcessName} {ArgsList}' was canceled for {Target} after {ElapsedTime}",
                        _processName, string.Join(" ", process.StartInfo.ArgumentList), target, elapsedTime);
                }
                else
                {
                    _logger.LogError(ex, "Failed to execute command '{ProcessName} {ArgsList}' for {Target} after {ElapsedTime}",
                        _processName, string.Join(" ", process.StartInfo.ArgumentList), target, elapsedTime);
                }
                return 1;
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
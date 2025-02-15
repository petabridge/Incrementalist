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

        public RunDotNetCommandTask(BuildSettings settings, ILogger logger, string[] dotnetArgs, bool continueOnError, bool runInParallel)
        {
            _settings = settings;
            _logger = logger;
            _dotnetArgs = dotnetArgs;
            _continueOnError = continueOnError;
            _runInParallel = runInParallel;
        }

        public async Task<int> Run(IEnumerable<string> affectedProjects)
        {
            var projects = affectedProjects.ToList();
            if (!projects.Any())
            {
                _logger.LogInformation("No affected projects to run commands against.");
                return 0;
            }

            _logger.LogInformation("Running '{0}' against {1} affected projects", string.Join(" ", _dotnetArgs), projects.Count);
            
            var failedProjects = new List<string>();
            
            async Task<bool> RunCommand(string project)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"{string.Join(" ", _dotnetArgs)} {project}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        _logger.LogInformation("[{0}] {1}", project, args.Data);
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        _logger.LogError("[{0}] {1}", project, args.Data);
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        _logger.LogError("Command failed for project {0} with exit code {1}", project, process.ExitCode);
                        return false;
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute command for project {0}", project);
                    return false;
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (_runInParallel)
            {
                var tasks = projects.Select(async project =>
                {
                    if (!await RunCommand(project) && !_continueOnError)
                        failedProjects.Add(project);
                });
                
                await Task.WhenAll(tasks);
            }
            else
            {
                foreach (var project in projects)
                {
                    if (!await RunCommand(project) && !_continueOnError)
                    {
                        failedProjects.Add(project);
                        break;
                    }
                }
            }

            if (failedProjects.Any())
            {
                _logger.LogError("Command failed for the following projects: {0}", string.Join(", ", failedProjects));
                return 1;
            }

            return 0;
        }
    }
} 
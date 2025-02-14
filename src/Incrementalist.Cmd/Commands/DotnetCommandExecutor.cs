using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd.Commands
{
    public class DotnetCommandExecutor
    {
        private readonly ILogger _logger;
        private readonly SlnOptions _options;

        public DotnetCommandExecutor(SlnOptions options, ILogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ExecuteCommandsOnProjects(IEnumerable<string> projectPaths)
        {
            if (_options.DotnetCommands == null || !_options.DotnetCommands.Any())
            {
                _logger.LogInformation("No dotnet commands specified. Skipping execution.");
                return true;
            }

            var success = true;
            foreach (var projectPath in projectPaths)
            {
                foreach (var command in _options.DotnetCommands)
                {
                    success &= await ExecuteCommand(command.Trim(), projectPath);
                    if (!success)
                    {
                        _logger.LogError($"Command '{command}' failed for project {projectPath}. Stopping execution.");
                        return false;
                    }
                }
            }

            return success;
        }

        private async Task<bool> ExecuteCommand(string command, string projectPath)
        {
            var args = new List<string> { command, projectPath };

            // Add configuration if specified
            if (!string.IsNullOrEmpty(_options.Configuration))
            {
                args.Add("--configuration");
                args.Add(_options.Configuration);
            }

            // Add framework if specified
            if (!string.IsNullOrEmpty(_options.Framework))
            {
                args.Add("--framework");
                args.Add(_options.Framework);
            }

            // Add no-restore if specified
            if (_options.NoRestore)
            {
                args.Add("--no-restore");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            _logger.LogInformation($"Executing: dotnet {process.StartInfo.Arguments}");

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogInformation(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogError(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
    }
}
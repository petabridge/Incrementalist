// -----------------------------------------------------------------------
// <copyright file="SlnOptions.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using CommandLine;

namespace Incrementalist.Cmd
{
    [Verb("create-config", HelpText = "Create a new configuration file with current options.")]
    public sealed class CreateConfigOptions : SlnOptions
    {
        internal override SlnOptions PrepareForMerge()
        {
            return new CreateConfigOptions()
            {
                ConfigFile = ConfigFile
            };
        }
    }

    [Verb("list-affected-folders", HelpText = "List affected folders instead of .NET projects.")]
    public sealed class ListFoldersOptions : SlnOptions
    {
        internal override SlnOptions PrepareForMerge()
        {
            return new ListFoldersOptions()
            {
                ConfigFile = ConfigFile
            };
        }
    }

    [Verb("run", HelpText = "Run a command against affected projects. Use the `--dry` option to test without executing.")]
    public sealed class RunOptions : SlnOptions
    {
        // Property to store dotnet CLI arguments that come after --
        public string[] DotNetArgs { get; set; } = [];

        [Option("dry", HelpText = "Performs a dry run without executing any commands. Useful for testing.",
            Required = false)]
        public bool DryRun { get; set; } = false;

        internal override SlnOptions PrepareForMerge()
        {
            return new RunOptions
            {
                DotNetArgs = DotNetArgs,
                ConfigFile = ConfigFile,
                DryRun = DryRun,
            };
        }
    }

    public abstract class SlnOptions
    {
        [Option('s', "sln",
            HelpText =
                "The name of the Solution file to be analyzed by Incrementalist. Defaults to '*.sln' in the current directory.",
            Required = false)]
        public string? SolutionFilePath { get; set; }

        [Option('f', "file", HelpText = "If specified, writes the output to the named file.", Required = false)]
        public string? OutputFile { get; set; }

        [Option('b', "branch", HelpText = "The git branch to compare against, i.e. 'dev' or 'master'.",
            Required = false)]
        public string? GitBranch { get; set; }

        [Option('d', "dir",
            HelpText = "Specify the working directory explicitly. Defaults to using the current working directory.")]
        public string? WorkingDirectory { get; set; }

        [Option(
            Default = false,
            HelpText = "Prints out extensive debug logs during operation.")]
        public bool Verbose { get; set; }

        [Option('t', "timeout", Default = 2,
            HelpText = "Specifies the load timeout for the solution in whole minutes. Defaults to 2 minutes.")]
        public int TimeoutMinutes { get; set; }

        [Option('c', "config",
            HelpText =
                "Path to the configuration file. Defaults to .incrementalist/incrementalist.json in the current directory.",
            Required = false)]
        public string? ConfigFile { get; set; }

        [Option("continue-on-error", HelpText = "When running commands, continue executing even if some commands fail.",
            Default = true)]
        public bool ContinueOnError { get; set; }

        [Option("parallel", HelpText = "When running commands, execute them in parallel.", Default = false)]
        public bool RunInParallel { get; set; }

        [Option("fail-on-no-projects", HelpText = "When running commands, fail if no projects are affected.",
            Default = false)]
        public bool FailOnNoProjects { get; set; }

        /*
         * NOTE: while working on https://github.com/petabridge/Incrementalist/issues/366 we discovered that CommandLineParser
         * has to work with `IEnumerable<string>` for repeatable options, not `string[]`.
         */
        [Option("skip-glob",
            HelpText = "Glob pattern to exclude projects from the final list. Applied after analyzing dependencies.",
            Required = false)]
        public IEnumerable<string>? SkipGlobs { get; set; }

        [Option("target-glob",
            HelpText =
                "Glob pattern to include only matching projects in the final list. Applied after analyzing dependencies.",
            Required = false)]
        public IEnumerable<string>? TargetGlobs { get; set; }
        
        /// <summary>
        /// Needed for configuration merging.
        /// </summary>
        internal abstract SlnOptions PrepareForMerge();
    }
}
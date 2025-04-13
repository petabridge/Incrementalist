// -----------------------------------------------------------------------
// <copyright file="SlnOptions.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using CommandLine;

namespace Incrementalist.Cmd
{
    public sealed class SlnOptions
    {
        [Option('s', "sln", HelpText = "The name of the Solution file to be analyzed by Incrementalist. Defaults to '*.sln' in the current directory.",
            Required = false)]
        public string? SolutionFilePath { get; set; }

        [Option('f', "file", HelpText = "If specified, writes the output to the named file.", Required = false)]
        public string? OutputFile { get; set; }

        [Option('l', "folders-only", HelpText = "List affected folders instead of .NET projects", Required = false,
            Default = false)]
        public bool ListFolders { get; set; }

        [Option('b', "branch", HelpText = "The git branch to compare against, i.e. 'dev' or 'master'.",
            Required = false)]
        public string? GitBranch { get; set; }

        [Option('d', "dir", HelpText = "Specify the working directory explicitly. Defaults to using the current working directory.")]
        public string? WorkingDirectory { get; set; }

        [Option(
            Default = false,
            HelpText = "Prints out extensive debug logs during operation.")]
        public bool Verbose { get; set; }

        [Option('t', "timeout", Default = 2, HelpText = "Specifies the load timeout for the solution in whole minutes. Defaults to 2 minutes.")]
        public int TimeoutMinutes { get; set; }

        [Option('r', "run", HelpText = "Run a dotnet CLI command against affected projects. All arguments after -- will be passed to dotnet.", Required = false)]
        public bool RunCommand { get; set; }

        [Option("continue-on-error", HelpText = "When running commands, continue executing even if some commands fail.", Default = true)]
        public bool ContinueOnError { get; set; }

        [Option("parallel", HelpText = "When running commands, execute them in parallel.", Default = false)]
        public bool RunInParallel { get; set; }

        [Option("fail-on-no-projects", HelpText = "When running commands, fail if no projects are affected.", Default = false)]
        public bool FailOnNoProjects { get; set; }

        [Option("no-cache", HelpText = "Ignore any existing cache file and perform a full Roslyn analysis.", Default = false)]
        public bool NoCache { get; set; }

        [Option('c', "config", HelpText = "Path to the configuration file. Defaults to .incrementalist/incrementalist.json in the current directory.", Required = false)]
        public string? ConfigFile { get; set; }

        [Option("create-config", HelpText = "Create a new configuration file with current options. If config file path is specified with -c, that path will be used; otherwise default path (.incrementalist/incrementalist.json) is used.", Required = false)]
        public bool CreateConfig { get; set; }
        
        /*
         * NOTE: while working on https://github.com/petabridge/Incrementalist/issues/366 we discovered that CommandLineParser
         * has to work with `IEnumerable<string>` for repeatable options, not `string[]`.
         */

        [Option("skip-glob", HelpText = "Glob pattern to exclude projects from the final list. Applied after analyzing dependencies.", Required = false)]
        public IEnumerable<string>? SkipGlobs { get; set; }

        [Option("target-glob", HelpText = "Glob pattern to include only matching projects in the final list. Applied after analyzing dependencies.", Required = false)]
        public IEnumerable<string>? TargetGlobs { get; set; }

        // Property to store dotnet CLI arguments that come after --
        public string[] DotNetArgs { get; set; } = [];
    }
}
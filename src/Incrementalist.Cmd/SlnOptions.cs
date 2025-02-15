// -----------------------------------------------------------------------
// <copyright file="SlnOptions.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using CommandLine;
using System.Collections.Generic;

namespace Incrementalist.Cmd
{
    public sealed class SlnOptions
    {
        [Option('s', "sln", HelpText = "The name of the Solution file to be analyzed by Incrementalist.",
            Required = false)]
        public string SolutionFilePath { get; set; }

        [Option('f', "file", HelpText = "If specified, writes the output to the named file.", Required = false)]
        public string OutputFile { get; set; }

        [Option('l', "folders-only", HelpText = "List affected folders instead of .NET projects", Required = false)]
        public bool ListFolders { get; set; }

        [Option('b', "branch", HelpText = "The git branch to compare against. i.e. the `dev` or the `master` branch.",
            Required = true, Default = "dev")]
        public string GitBranch { get; set; }

        [Option('d', "dir", HelpText = "Specify the working directory explicitly. Defaults to using the current working directory.")]
        public string WorkingDirectory { get; set; }

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

        // Property to store dotnet CLI arguments that come after --
        public string[] DotNetArgs { get; set; }
    }
}
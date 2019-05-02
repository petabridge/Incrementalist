// -----------------------------------------------------------------------
// <copyright file="SlnOptions.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using CommandLine;

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

        [Option(
            Default = false,
            HelpText = "Prints out extensive debug logs during operation.")]
        public bool Verbose { get; set; }
    }
}
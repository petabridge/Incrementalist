// -----------------------------------------------------------------------
// <copyright file="SlnOptionsParsingSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Incrementalist.Cmd;
using Incrementalist.Cmd.Config;
using Microsoft.CodeAnalysis;
using Xunit;
using static Incrementalist.Cmd.SlnOptionsParser;

namespace Incrementalist.Tests.Config;

public class SlnOptionsParsingSpecs
{
    /// <summary>
    /// Reproduction spec for https://github.com/petabridge/Incrementalist/issues/378 
    /// </summary>
    [Theory]
    [InlineData("run --config -- build -c Release", "build -c Release")]
    [InlineData("run --dry --b dev -- build -c Release", "build -c Release")]
    [InlineData("run -c -- build -c Release", "build -c Release")]
    [InlineData(@"run --config -b dev -f C:\user\output.txt -- build -c Release", "build -c Release")]
    [InlineData(@"create-config --config C:\user\config.json -b dev --skip-glob **/*.Tests.csproj **/tests/*.csproj", null)]
    [InlineData(@"create-config", null)]
    [InlineData("list-affected-folders", null)]
    public void ShouldSeparateDotnetArgsFromSlnOptions(string cliArg, string? expectedDotNetArgs)
    {
        var args = CommandLineParser
            .SplitCommandLineIntoArguments(cliArg, true).ToArray();
        var r = TryParseSlnOptions(args, out SlnOptions? result);

        Assert.Equal(0, r);
        Assert.NotNull(result);

        if (expectedDotNetArgs != null)
        {
            var runOptions = result as RunOptions;
            Assert.NotNull(runOptions);
            
            Assert.Equal(expectedDotNetArgs, string.Join(" ", runOptions.DotNetArgs));
        }
    }
    
    /*
     *  Looking for a suspected bug where:
     * 
     *  1. No globs specified on command line
     *  2. globs specified in config file
     *  3. Parse operation supplies an empty array instead of a null array
     *  4. Merge operation fails because it uses the empty array instead of the config file globs
     */
    [Fact]
    public void ShouldKeepConfigGlobs()
    {
        string[] expectedGlobs = ["**/*.Tests.csproj", "**/tests/*.csproj"];
        var args = CommandLineParser
            .SplitCommandLineIntoArguments("run", true).ToArray();
        var r = TryParseSlnOptions(args, out SlnOptions? result);

        Assert.Equal(0, r);
        Assert.NotNull(result);
        
        var config = new IncrementalistConfig
        {
            TargetGlob = null,
            SkipGlob = expectedGlobs,
            GitBranch = "dev",
            WorkingDirectory = @"C:\user\workingdir"
        };
        
        var merged = ConfigMerger.Merge(result, config);
        Assert.Equivalent(expectedGlobs, merged.SkipGlobs);
    }
}

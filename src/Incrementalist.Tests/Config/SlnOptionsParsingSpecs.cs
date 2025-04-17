// -----------------------------------------------------------------------
// <copyright file="SlnOptionsParsingSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using Incrementalist.Cmd;
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
    [InlineData("run --config -- build -c Release")]
    [InlineData("run --dry --b dev -- build -c Release")]
    [InlineData("run -c -- build -c Release")]
    [InlineData(@"run --config -b dev -f C:\user\output.txt -- build -c Release")]
    [InlineData(@"create-config --config C:\user\config.json -b dev --skip-glob **/*.Tests.csproj **/tests/*.csproj")]
    [InlineData(@"create-config")]
    [InlineData("list-affected-folders")]
    public void ShouldSeparateDotnetArgsFromSlnOptions(string cliArg)
    {
        var args = CommandLineParser
            .SplitCommandLineIntoArguments(cliArg, true).ToArray();
        var r = TryParseSlnOptions(args, out SlnOptions? result);

        Assert.Equal(0, r);
        Assert.NotNull(result);
    }
}
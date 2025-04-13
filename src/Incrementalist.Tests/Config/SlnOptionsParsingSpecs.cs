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
    [InlineData("--config -r -- build -c Release")]
    public void ShouldSeparateDotnetArgsFromSlnOptions(string cliArg)
    {
        var args = CommandLineParser
            .SplitCommandLineIntoArguments(cliArg, true).ToArray();
        var r = TryParseSlnOptions(args, out SlnOptions? result);
        
        Assert.Equal(0, r);
        Assert.NotNull(result);
    }
}
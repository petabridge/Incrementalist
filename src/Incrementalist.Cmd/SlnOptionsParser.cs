using System;
using System.Linq;
using CommandLine;

namespace Incrementalist.Cmd;

/// <summary>
/// Tries to separate the `dotnet` executable arguments from the <see cref="SlnOptions"/> arguments
/// </summary>
public static class SlnOptionsParser
{
    public static int TryParseSlnOptions(string[] args, out SlnOptions? result)
    {
        // Split args at -- to separate incrementalist args from dotnet args
        var splitIndex = Array.IndexOf(args, "--");
        var incrementalistArgs = splitIndex >= 0 ? args.Take(splitIndex).ToArray() : args;
        var dotnetArgs = splitIndex >= 0 ? args.Skip(splitIndex + 1).ToArray() : [];

        SlnOptions? options = null;
            
        var r = Parser.Default.ParseArguments<SlnOptions>(incrementalistArgs).MapResult(r =>
        {
            options = r;
            options.DotNetArgs = dotnetArgs;
            return 0;
        }, _ => 1);
            
        result = options;
            
        return r;
    }
}
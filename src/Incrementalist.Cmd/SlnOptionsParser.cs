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
        try
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

            if (r != 0)
                DebugDump(null);

            return r;
        }
        catch (Exception ex)
        {
            DebugDump(ex);
            result = null;
            return 1;
        }

        void DebugDump(Exception? ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"Error parsing command line arguments: {ex.Message}");
                Console.Write(ex.StackTrace);
            }
            else
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Error parsing command line arguments");
                Console.WriteLine("Did you try to run `dotnet tool run incrementalist`? This has parsing issues: https://github.com/petabridge/Incrementalist/issues/378");
                Console.WriteLine("Please run `dotnet incrementalist` directly instead if you're using local dotnet tools.");
                Console.ForegroundColor = originalColor;
            }
                
#if DEBUG
            Console.Write(Environment.NewLine);
            Console.WriteLine("Raw CLI string:");
            Console.Write(Environment.NewLine);
            Console.WriteLine(Environment.CommandLine);
            Console.WriteLine("Raw args [parsed by dotnet]: ");
            Console.WriteLine(string.Join(", ", Environment.GetCommandLineArgs()));
#endif
        }
    }
}
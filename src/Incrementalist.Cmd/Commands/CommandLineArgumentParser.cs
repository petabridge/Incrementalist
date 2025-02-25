using System;
using System.Collections.Generic;
using System.Text;

namespace Incrementalist.Cmd.Commands
{
    /// <summary>
    /// Handles parsing of complex command line arguments with proper quote handling
    /// </summary>
    public static class CommandLineArgumentParser
    {
        /// <summary>
        /// Combines command line arguments into a properly quoted string
        /// </summary>
        public static string CombineArguments(IEnumerable<string> arguments)
        {
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            
            var sb = new StringBuilder();
            foreach (var arg in arguments)
            {
                if (sb.Length > 0)
                    sb.Append(' ');

                // Check if we need to quote this argument
                var needsQuoting = arg.Contains(' ') || arg.Contains('\t') || arg.Contains('"');
                
                if (!needsQuoting)
                {
                    sb.Append(arg);
                }
                else
                {
                    sb.Append('"');
                    
                    // Replace any embedded quotes with escaped quotes
                    sb.Append(arg.Replace("\"", "\\\""));
                    
                    sb.Append('"');
                }
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Splits a command line string into individual arguments, preserving quoted sections
        /// </summary>
        public static string[] SplitArguments(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine)) return Array.Empty<string>();

            var args = new List<string>();
            var currentArg = new StringBuilder();
            var inQuotes = false;
            var escaped = false;

            for (var i = 0; i < commandLine.Length; i++)
            {
                var c = commandLine[i];

                if (escaped)
                {
                    currentArg.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (currentArg.Length > 0)
                    {
                        args.Add(currentArg.ToString());
                        currentArg.Clear();
                    }
                    continue;
                }

                currentArg.Append(c);
            }

            if (currentArg.Length > 0)
                args.Add(currentArg.ToString());

            return args.ToArray();
        }
    }
} 
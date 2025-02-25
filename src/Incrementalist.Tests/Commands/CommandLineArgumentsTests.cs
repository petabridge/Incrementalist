using System;
using System.Linq;
using Xunit;

namespace Incrementalist.Tests.Commands
{
    public class CommandLineArgumentsTests
    {
        private string CombineArgs(params string[] args)
        {
            // This is the same logic used in RunDotNetCommandTask
            return string.Join(" ", args.Select(arg => 
            {
                if (arg.Contains(' ') || arg.Contains('"'))
                {
                    return $"\"{arg.Replace("\"", "\\\"")}\"";
                }
                return arg;
            }));
        }

        [Fact]
        public void Should_Not_Quote_Simple_Arguments()
        {
            var result = CombineArgs("test", "-c", "Release", "--no-build");
            Assert.Equal("test -c Release --no-build", result);
        }

        [Fact]
        public void Should_Quote_Arguments_With_Spaces()
        {
            var result = CombineArgs("test", "--collect:XPlat Code Coverage");
            Assert.Equal("test \"--collect:XPlat Code Coverage\"", result);
        }

        [Fact]
        public void Should_Escape_Embedded_Quotes()
        {
            var result = CombineArgs("test", "--logger:\"trx\"");
            Assert.Equal("test \"--logger:\\\"trx\\\"\"", result);
        }

        [Fact]
        public void Should_Handle_Complex_Arguments()
        {
            var result = CombineArgs(
                "test",
                "--collect:\"XPlat Code Coverage\"",
                "--logger:\"console;verbosity=detailed\"",
                "--results-directory:\"Test Results\""
            );

            Assert.Equal(
                "test " +
                "\"--collect:\\\"XPlat Code Coverage\\\"\" " +
                "\"--logger:\\\"console;verbosity=detailed\\\"\" " +
                "\"--results-directory:\\\"Test Results\\\"\"", 
                result);
        }

        [Fact]
        public void Should_Handle_Windows_Paths()
        {
            var result = CombineArgs(
                "test",
                "--results-directory:C:\\Test Results",
                "--logger:trx;LogFileName=C:\\Test Results\\test.trx"
            );

            Assert.Equal(
                "test " +
                "\"--results-directory:C:\\Test Results\" " +
                "\"--logger:trx;LogFileName=C:\\Test Results\\test.trx\"",
                result);
        }

        [Fact]
        public void Should_Handle_Mixed_Path_Separators()
        {
            var result = CombineArgs(
                "test",
                "--results-directory:C:/Test Results",
                "--logger:trx;LogFileName=C:/Test Results/test.trx"
            );

            Assert.Equal(
                "test " +
                "\"--results-directory:C:/Test Results\" " +
                "\"--logger:trx;LogFileName=C:/Test Results/test.trx\"",
                result);
        }
    }
} 
using System;
using System.Linq;
using Incrementalist.Cmd.Commands;
using Xunit;

namespace Incrementalist.Tests
{
    public class CommandLineArgumentParserTests
    {
        [Fact]
        public void CombineArguments_WithSimpleArgs_ShouldNotQuote()
        {
            var args = new[] { "test", "-c", "Release", "--no-build" };
            var result = CommandLineArgumentParser.CombineArguments(args);
            Assert.Equal("test -c Release --no-build", result);
        }

        [Fact]
        public void CombineArguments_WithSpaces_ShouldQuote()
        {
            var args = new[] { "test", "--collect:\"XPlat Code Coverage\"", "--logger:\"console;verbosity=normal\"" };
            var result = CommandLineArgumentParser.CombineArguments(args);
            Assert.Equal("test \"--collect:\\\"XPlat Code Coverage\\\"\" \"--logger:\\\"console;verbosity=normal\\\"\"", result);
        }

        [Fact]
        public void SplitArguments_WithQuotedSpaces_ShouldPreserveQuotes()
        {
            var input = "test -c Release --no-build \"--collect:XPlat Code Coverage\" \"--logger:console;verbosity=normal\"";
            var result = CommandLineArgumentParser.SplitArguments(input);
            
            Assert.Equal(new[]
            {
                "test",
                "-c",
                "Release",
                "--no-build",
                "--collect:XPlat Code Coverage",
                "--logger:console;verbosity=normal"
            }, result);
        }

        [Fact]
        public void SplitArguments_WithEscapedQuotes_ShouldHandleCorrectly()
        {
            var input = "test \"--logger:\\\"trx\\\"\" \"--collect:\\\"XPlat Code Coverage\\\"\"";
            var result = CommandLineArgumentParser.SplitArguments(input);
            
            Assert.Equal(new[]
            {
                "test",
                "--logger:\"trx\"",
                "--collect:\"XPlat Code Coverage\""
            }, result);
        }

        [Fact]
        public void CombineAndSplit_ShouldBeSymmetric()
        {
            var originalArgs = new[]
            {
                "test",
                "-c",
                "Release",
                "--no-build",
                "--logger:\"trx\"",
                "--collect:\"XPlat Code Coverage\"",
                "--logger:\"console;verbosity=normal\"",
                "--results-directory:TestResults"
            };

            var combined = CommandLineArgumentParser.CombineArguments(originalArgs);
            var split = CommandLineArgumentParser.SplitArguments(combined);

            Assert.Equal(originalArgs, split);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SplitArguments_WithEmptyInput_ReturnsEmptyArray(string input)
        {
            var result = CommandLineArgumentParser.SplitArguments(input);
            Assert.Empty(result);
        }

        [Fact]
        public void CombineArguments_WithNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CommandLineArgumentParser.CombineArguments(null));
        }
    }
} 
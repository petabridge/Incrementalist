using Incrementalist.Cmd;
using Xunit;

public class SlnOptionsTests
{

    [Fact]
    public void CompareTarget_Should_Return_CommitSha_When_Provided()
    {
        // Arrange
        var options = new RunOptions
        {
            CompareSha = "abc123",
            GitBranch = "main"
        };

        // Act
        var target = options.CompareTarget;

        // Assert
        Assert.Equal("abc123", target);
    }

    [Fact]
    public void CompareTarget_Should_Return_GitBranch_When_CommitSha_Is_Null()
    {
        // Arrange
        var options = new RunOptions
        {
            GitBranch = "main"
        };

        // Act
        var target = options.CompareTarget;

        // Assert
        Assert.Equal("main", target);
    }

    [Fact]
    public void CompareTarget_Should_Return_Null_When_Both_Are_Null()
    {
        // Arrange
        var options = new RunOptions();

        // Act
        var target = options.CompareTarget;

        // Assert
        Assert.Null(target);
    }
}
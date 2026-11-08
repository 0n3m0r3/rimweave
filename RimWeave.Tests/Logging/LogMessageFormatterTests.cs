using RimWeave.Logging;
using Xunit;

namespace RimWeave.Tests.Logging;

public sealed class LogMessageFormatterTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Format_PrefixesEveryLine(string lineEnding)
    {
        string result = LogMessageFormatter.Format($"first{lineEnding}second");

        Assert.Equal("[RimWeave] first\n[RimWeave] second", result);
    }

    [Fact]
    public void Format_NormalizesMixedLineEndings()
    {
        string result = LogMessageFormatter.Format("first\r\nsecond\rthird\nfourth");

        Assert.Equal(
            "[RimWeave] first\n[RimWeave] second\n[RimWeave] third\n[RimWeave] fourth",
            result);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("ready\n")]
    [InlineData("ready\r\n")]
    [InlineData("ready\r\n\r\n\n")]
    public void Format_DoesNotAddTrailingEmptyLines(string message)
    {
        string result = LogMessageFormatter.Format(message);

        Assert.Equal("[RimWeave] ready", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n\n\r")]
    public void Format_ReturnsPrefixForEmptyMessage(string message)
    {
        string result = LogMessageFormatter.Format(message);

        Assert.Equal("[RimWeave] ", result);
    }

    [Fact]
    public void Format_PreservesIndentationAndInternalEmptyLines()
    {
        string result = LogMessageFormatter.Format("\n  first  \n\n\tlast");

        Assert.Equal(
            "[RimWeave] \n[RimWeave]   first  \n[RimWeave] \n[RimWeave] \tlast",
            result);
    }
}
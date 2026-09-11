namespace CalendarCli.Tests;

public sealed class CliArgumentsTests
{
    [Theory]
    [InlineData("--duration", "01:30:00")]
    [InlineData("--end", "tomorrow 3PM")]
    public void Parse_EventBoundaryOption_ReturnsValue(string optionName, string optionValue)
    {
        var parsed = CliArguments.Parse([optionName, optionValue]);

        Assert.Equal(optionValue, parsed.GetSingleValue(optionName));
    }

    [Fact]
    public void Parse_AllDayFlag_ReturnsTrueWithoutValue()
    {
        var parsed = CliArguments.Parse(["--all-day"]);

        Assert.Equal("true", parsed.GetSingleValue("--all-day"));
    }
}
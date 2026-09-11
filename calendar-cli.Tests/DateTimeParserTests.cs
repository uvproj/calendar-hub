namespace CalendarCli.Tests;

public sealed class DateTimeParserTests
{
    [Theory]
    [InlineData("9/19/2026", 2026, 9, 19, 0, 0)]
    [InlineData("9/19/2026:2:30 PM", 2026, 9, 19, 14, 30)]
    [InlineData("9/19/2026:14:30", 2026, 9, 19, 14, 30)]
    [InlineData("2026-06-18T14:30", 2026, 6, 18, 14, 30)]
    public void TryParse_AbsoluteFormat_ReturnsExpectedLocalDateTime(
        string value,
        int year,
        int month,
        int day,
        int hour,
        int minute)
    {
        var parsed = DateTimeParser.TryParse(value, out var result);

        Assert.True(parsed);
        Assert.Equal(year, result.Year);
        Assert.Equal(month, result.Month);
        Assert.Equal(day, result.Day);
        Assert.Equal(hour, result.Hour);
        Assert.Equal(minute, result.Minute);
    }

    [Theory]
    [InlineData("today", 0, 0, 0)]
    [InlineData("tomorrow", 1, 0, 0)]
    [InlineData("next week", 7, 0, 0)]
    [InlineData("tomorrow 2PM", 1, 14, 0)]
    [InlineData("today 14:30", 0, 14, 30)]
    public void TryParse_RelativeExpression_UsesProvidedCurrentDate(
        string value,
        int daysFromToday,
        int hour,
        int minute)
    {
        var now = new DateTimeOffset(2026, 9, 10, 11, 45, 0, TimeSpan.FromHours(5.5));
        var expected = new DateTimeOffset(2026, 9, 10, hour, minute, 0, now.Offset).AddDays(daysFromToday);

        var parsed = DateTimeParser.TryParse(value, now, out var result);

        Assert.True(parsed);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("tomorrow sometime")]
    [InlineData("next week afternoon")]
    [InlineData("not a date")]
    public void TryParse_InvalidValue_ReturnsFalse(string value)
    {
        var now = new DateTimeOffset(2026, 9, 10, 11, 45, 0, TimeSpan.Zero);

        var parsed = DateTimeParser.TryParse(value, now, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseMonth_YearMonth_ReturnsFirstDayOfMonth()
    {
        var parsed = DateTimeParser.TryParseMonth("2026-09", out var result);

        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 9, 1), result);
    }

    [Fact]
    public void Format_LocalDateTime_ReturnsExpectedDisplayValue()
    {
        var localDateTime = new DateTime(2026, 9, 19, 14, 30, 0, DateTimeKind.Local);
        var value = new DateTimeOffset(localDateTime);
        var expected = $"2026-09-19 14:30 {value:zzz}";

        var result = DateTimeParser.Format(value);

        Assert.Equal(expected, result);
    }
}

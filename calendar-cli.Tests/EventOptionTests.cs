namespace CalendarCli.Tests;

public sealed class EventOptionTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 9, 10, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void CreateEventRequest_Duration_SetsTimedExclusiveEnd()
    {
        var parsed = CliArguments.Parse(["--duration", "01:30:00"]);

        var request = CalendarConsole.CreateEventRequest(parsed, "Planning", StartsAt, []);

        Assert.Equal(StartsAt.AddMinutes(90), request.EndsAt);
    }

    [Fact]
    public void CreateEventRequest_End_SetsTimedExclusiveEnd()
    {
        var parsed = CliArguments.Parse(["--end", "9/10/2026:11:00"]);

        var request = CalendarConsole.CreateEventRequest(parsed, "Planning", StartsAt.ToLocalTime(), []);

        Assert.Equal(new DateTimeOffset(2026, 9, 10, 11, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 9, 10))), request.EndsAt);
    }

    [Fact]
    public void CreateEventRequest_AllDay_NormalizesToOneLocalDay()
    {
        var parsed = CliArguments.Parse(["--all-day"]);

        var request = CalendarConsole.CreateEventRequest(parsed, "Holiday", StartsAt, []);

        Assert.True(request.IsAllDay);
        Assert.Equal(TimeSpan.Zero, request.StartsAt.ToLocalTime().TimeOfDay);
        Assert.Equal(request.StartsAt.ToLocalTime().Date.AddDays(1), request.EndsAt.ToLocalTime().Date);
    }

    [Fact]
    public void CreateEventRequest_EndAndDuration_ThrowsMutuallyExclusiveError()
    {
        var parsed = CliArguments.Parse([
            "--end", "9/10/2026:11:00", "--duration", "01:30:00"
        ]);

        var exception = Assert.Throws<ArgumentException>(() =>
            CalendarConsole.CreateEventRequest(parsed, "Planning", StartsAt, []));

        Assert.Equal("The --end and --duration options are mutually exclusive.", exception.Message);
    }

    [Fact]
    public void CreateEventRequest_AllDayPartialDayDuration_ThrowsUsageError()
    {
        var parsed = CliArguments.Parse(["--all-day", "--duration", "12:00:00"]);

        var exception = Assert.Throws<ArgumentException>(() =>
            CalendarConsole.CreateEventRequest(parsed, "Holiday", StartsAt, []));

        Assert.Equal("An all-day event duration must be a whole number of days.", exception.Message);
    }
}
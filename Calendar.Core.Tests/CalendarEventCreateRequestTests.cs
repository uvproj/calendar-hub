namespace Calendar.Core.Tests;

public sealed class CalendarEventCreateRequestTests
{
    [Fact]
    public void Constructor_EndEqualsStart_ThrowsArgumentException()
    {
        var startsAt = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => new CalendarEventCreateRequest(
            "Invalid",
            null,
            null,
            startsAt,
            startsAt,
            false,
            []));
    }

    [Fact]
    public void Constructor_AllDayStartNotAtMidnight_ThrowsArgumentException()
    {
        var startsAt = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => new CalendarEventCreateRequest(
            "Invalid",
            null,
            null,
            startsAt,
            startsAt.AddDays(1),
            true,
            []));
    }
}
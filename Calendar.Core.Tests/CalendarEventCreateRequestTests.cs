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

    [Fact]
    public void Constructor_Invitees_DefensivelyCopiesCollection()
    {
        var startsAt = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);
        var invitees = new List<string> { "first@example.com" };
        var request = new CalendarEventCreateRequest(
            "Planning",
            null,
            null,
            startsAt,
            startsAt.AddHours(1),
            false,
            invitees);

        invitees.Add("later@example.com");

        Assert.Equal(["first@example.com"], request.Invitees);
    }
}
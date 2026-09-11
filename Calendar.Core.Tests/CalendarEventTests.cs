namespace Calendar.Core.Tests;

public sealed class CalendarEventTests
{
    [Fact]
    public void Constructor_Invitees_DefensivelyCopiesCollection()
    {
        var startsAt = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);
        var invitees = new List<string> { "first@example.com" };
        var calendarEvent = new CalendarEvent(
            "provider-id",
            "Planning",
            null,
            null,
            startsAt,
            startsAt.AddHours(1),
            false,
            invitees);

        invitees.Add("later@example.com");

        Assert.Equal(["first@example.com"], calendarEvent.Invitees);
    }
}
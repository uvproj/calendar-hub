using Google.Apis.Calendar.v3.Data;

namespace Calendar.Core.Tests;

public sealed class GoogleCalendarEventMapperTests
{
    [Fact]
    public void ToGoogleEvent_TimedRequest_MapsStartAndEndDateTimes()
    {
        var startsAt = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.FromHours(2));
        var request = new CalendarEventCreateRequest(
            "Planning",
            null,
            null,
            startsAt,
            startsAt.AddHours(1),
            false,
            []);

        var googleEvent = GoogleCalendarEventMapper.ToGoogleEvent("eventid", request);

        Assert.Equal(startsAt.AddHours(1), googleEvent.End.DateTimeDateTimeOffset);
    }

    [Fact]
    public void ToGoogleEvent_AllDayRequest_MapsExclusiveDateFields()
    {
        var startsAt = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var request = new CalendarEventCreateRequest(
            "Conference",
            null,
            null,
            startsAt,
            startsAt.AddDays(2),
            true,
            []);

        var googleEvent = GoogleCalendarEventMapper.ToGoogleEvent("eventid", request);

        Assert.Equal("2026-09-12", googleEvent.End.Date);
    }

    [Fact]
    public void ToCalendarEvent_AllDayGoogleEvent_ParsesExclusiveEnd()
    {
        var googleEvent = new Event
        {
            Id = "eventid",
            Summary = "Conference",
            Start = new EventDateTime { Date = "2026-09-10" },
            End = new EventDateTime { Date = "2026-09-12" }
        };

        var calendarEvent = GoogleCalendarEventMapper.ToCalendarEvent(googleEvent);

        Assert.Equal(new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero), calendarEvent.EndsAt);
    }
}
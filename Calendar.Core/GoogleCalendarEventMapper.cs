using System.Globalization;
using Google.Apis.Calendar.v3.Data;

namespace Calendar.Core;

internal static class GoogleCalendarEventMapper
{
    internal static Event ToGoogleEvent(string eventId, CalendarEventCreateRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(request);

        return new Event
        {
            Id = eventId,
            Summary = request.Name,
            Description = request.Description,
            Location = request.Location,
            Start = ToGoogleDateTime(request.StartsAt, request.IsAllDay),
            End = ToGoogleDateTime(request.EndsAt, request.IsAllDay),
            Attendees = request.Invitees
                .Select(email => new EventAttendee { Email = email })
                .ToList()
        };
    }

    internal static CalendarEvent ToCalendarEvent(Event googleEvent)
    {
        ArgumentNullException.ThrowIfNull(googleEvent);

        var isAllDay = !string.IsNullOrWhiteSpace(googleEvent.Start?.Date);
        var startsAt = FromGoogleDateTime(googleEvent.Start, isAllDay, "start");
        var endsAt = FromGoogleDateTime(googleEvent.End, isAllDay, "end");
        var invitees = googleEvent.Attendees?
            .Select(attendee => attendee.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!)
            .ToList() ?? [];

        return new CalendarEvent(
            googleEvent.Id ?? throw new InvalidDataException("The Google event has no identifier."),
            googleEvent.Summary ?? "Untitled Event",
            googleEvent.Description,
            googleEvent.Location,
            startsAt,
            endsAt,
            isAllDay,
            invitees);
    }

    private static EventDateTime ToGoogleDateTime(DateTimeOffset value, bool isAllDay)
    {
        if (isAllDay)
        {
            return new EventDateTime
            {
                Date = DateOnly.FromDateTime(value.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
        }

        return new EventDateTime
        {
            DateTimeDateTimeOffset = value
        };
    }

    private static DateTimeOffset FromGoogleDateTime(
        EventDateTime? googleDateTime,
        bool isAllDay,
        string boundaryName)
    {
        if (isAllDay && !string.IsNullOrWhiteSpace(googleDateTime?.Date))
        {
            var date = DateOnly.ParseExact(googleDateTime.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        return googleDateTime?.DateTimeDateTimeOffset
            ?? throw new InvalidDataException($"The Google event has no {boundaryName} date/time.");
    }
}
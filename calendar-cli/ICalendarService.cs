namespace CalendarCli;

internal interface ICalendarService
{
    Task AddAsync(CalendarEvent calendarEvent);
    Task<IReadOnlyList<CalendarEvent>> ListBetweenAsync(DateTimeOffset startInclusive, DateTimeOffset endExclusive);
    Task<(bool Succeeded, CalendarEvent? DeletedEvent)> DeleteAsync(Guid eventId);
}

namespace Calendar.Core.Tests;

internal sealed class FakeCalendarService : ICalendarService
{
    public Func<CalendarEventCreateRequest, CancellationToken, Task<CalendarEvent>> AddHandler { get; init; } =
        (_, _) => throw new NotSupportedException();

    public Func<DateTimeOffset, DateTimeOffset, CancellationToken, Task<IReadOnlyList<CalendarEvent>>> ListHandler { get; init; } =
        (_, _, _) => throw new NotSupportedException();

    public int AddCallCount { get; private set; }

    public int ListCallCount { get; private set; }

    public Task<CalendarEvent> AddAsync(
        CalendarEventCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        return AddHandler(request, cancellationToken);
    }

    public Task<IReadOnlyList<CalendarEvent>> ListBetweenAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        ListCallCount++;
        return ListHandler(startInclusive, endExclusive, cancellationToken);
    }

    public Task<(bool Succeeded, CalendarEvent? DeletedEvent)> DeleteAsync(
        string eventId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
namespace Calendar.Core;

/// <summary>
/// Provides calendar event persistence operations.
/// </summary>
public interface ICalendarService
{
    /// <summary>Creates an event from the specified request.</summary>
    /// <param name="request">The event creation request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created event, including its provider identifier.</returns>
    Task<CalendarEvent> AddAsync(
        CalendarEventCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lists events that overlap the specified half-open interval.</summary>
    /// <param name="startInclusive">The inclusive range start.</param>
    /// <param name="endExclusive">The exclusive range end.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching events ordered by start time.</returns>
    Task<IReadOnlyList<CalendarEvent>> ListBetweenAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an event by provider identifier.</summary>
    /// <param name="eventId">The provider event identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The deletion result and deleted event when found.</returns>
    Task<(bool Succeeded, CalendarEvent? DeletedEvent)> DeleteAsync(
        string eventId,
        CancellationToken cancellationToken = default);
}
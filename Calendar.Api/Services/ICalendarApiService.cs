using Calendar.Core;

namespace Calendar.Api.Services;

/// <summary>
/// Exposes Calendar Core operations used by the HTTP boundary and replaceable by integration tests.
/// </summary>
public interface ICalendarApiService
{
    /// <summary>Lists configured calendars without sensitive provider configuration.</summary>
    /// <returns>The safe calendar summaries.</returns>
    IReadOnlyList<CalendarServiceSummary> ListSummaries();

    /// <summary>Queries events from selected calendars.</summary>
    /// <param name="start">The inclusive range start.</param>
    /// <param name="end">The exclusive range end.</param>
    /// <param name="calendarNames">The optional selected calendars.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The source-aware events and safe per-calendar errors.</returns>
    Task<CalendarAggregationResult> QueryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<string>? calendarNames,
        CancellationToken cancellationToken);

    /// <summary>Creates a structured event batch in one destination calendar.</summary>
    /// <param name="destinationCalendar">The explicit destination calendar.</param>
    /// <param name="drafts">The validated event drafts.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The ordered per-item outcomes.</returns>
    Task<CalendarBatchCreationResult> CreateBatchAsync(
        string destinationCalendar,
        IReadOnlyList<CalendarEventCreateRequest> drafts,
        CancellationToken cancellationToken);
}
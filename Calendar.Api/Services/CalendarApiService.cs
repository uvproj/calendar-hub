using Calendar.Core;

namespace Calendar.Api.Services;

/// <summary>
/// Adapts Calendar Core application services to the HTTP boundary.
/// </summary>
public sealed class CalendarApiService : ICalendarApiService
{
    private readonly CalendarServiceFactory _factory;
    private readonly CalendarAggregationService _aggregationService;
    private readonly CalendarBatchCreationService _batchCreationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarApiService"/> class.
    /// </summary>
    /// <param name="factory">The configured calendar service factory.</param>
    /// <param name="aggregationService">The multi-calendar query service.</param>
    /// <param name="batchCreationService">The confirmed batch creation service.</param>
    public CalendarApiService(
        CalendarServiceFactory factory,
        CalendarAggregationService aggregationService,
        CalendarBatchCreationService batchCreationService)
    {
        _factory = factory;
        _aggregationService = aggregationService;
        _batchCreationService = batchCreationService;
    }

    /// <inheritdoc/>
    public IReadOnlyList<CalendarServiceSummary> ListSummaries() => _factory.ListSummaries();

    /// <inheritdoc/>
    public Task<CalendarAggregationResult> QueryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<string>? calendarNames,
        CancellationToken cancellationToken) =>
        _aggregationService.QueryAsync(start, end, calendarNames, cancellationToken);

    /// <inheritdoc/>
    public Task<CalendarBatchCreationResult> CreateBatchAsync(
        string destinationCalendar,
        IReadOnlyList<CalendarEventCreateRequest> drafts,
        CancellationToken cancellationToken) =>
        _batchCreationService.CreateAsync(destinationCalendar, drafts, cancellationToken);
}
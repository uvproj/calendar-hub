namespace Calendar.Core;

/// <summary>
/// Describes the outcome of one request in a calendar creation batch.
/// </summary>
/// <param name="Index">The zero-based index of the original request.</param>
/// <param name="Event">The created provider event when successful.</param>
/// <param name="Error">The safe failure details when unsuccessful.</param>
public sealed record CalendarBatchCreationItemResult(
    int Index,
    CalendarEvent? Event,
    CalendarOperationError? Error);

/// <summary>
/// Contains ordered item outcomes for a calendar creation batch.
/// </summary>
/// <param name="DestinationCalendarName">The canonical destination calendar name.</param>
/// <param name="DestinationCalendarType">The destination calendar provider type.</param>
/// <param name="Items">The item outcomes in original request order.</param>
public sealed record CalendarBatchCreationResult(
    string DestinationCalendarName,
    ServiceType DestinationCalendarType,
    IReadOnlyList<CalendarBatchCreationItemResult> Items);

/// <summary>
/// Creates a validated batch of events in one explicitly selected calendar.
/// </summary>
public sealed class CalendarBatchCreationService
{
    private const string ProviderErrorMessage = "Calendar provider operation failed.";
    private readonly CalendarServiceFactory _serviceFactory;

    /// <summary>Gets the largest accepted event creation batch.</summary>
    public const int MaxBatchSize = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarBatchCreationService"/> class.
    /// </summary>
    /// <param name="serviceFactory">The calendar service factory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceFactory"/> is <see langword="null"/>.</exception>
    public CalendarBatchCreationService(CalendarServiceFactory serviceFactory)
    {
        ArgumentNullException.ThrowIfNull(serviceFactory);
        _serviceFactory = serviceFactory;
    }

    /// <summary>
    /// Creates events sequentially in one explicitly selected destination calendar.
    /// </summary>
    /// <param name="destinationCalendarName">The configured destination calendar name.</param>
    /// <param name="requests">The structured event creation requests.</param>
    /// <param name="cancellationToken">A token that cancels the entire operation.</param>
    /// <returns>The canonical destination and ordered per-item outcomes.</returns>
    /// <exception cref="ArgumentException">The destination or request collection is invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="requests"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels the operation.</exception>
    public async Task<CalendarBatchCreationResult> CreateAsync(
        string destinationCalendarName,
        IReadOnlyList<CalendarEventCreateRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationCalendarName);
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            throw new ArgumentException("At least one event creation request is required.", nameof(requests));
        }

        if (requests.Count > MaxBatchSize)
        {
            throw new ArgumentException($"A batch cannot contain more than {MaxBatchSize} requests.", nameof(requests));
        }

        if (requests.Any(request => request is null))
        {
            throw new ArgumentException("Event creation requests cannot contain null items.", nameof(requests));
        }

        var normalizedDestination = destinationCalendarName.Trim();
        var summary = _serviceFactory.ListSummaries().FirstOrDefault(candidate =>
            string.Equals(candidate.Name, normalizedDestination, StringComparison.OrdinalIgnoreCase)) ??
            throw new ArgumentException($"Calendar '{normalizedDestination}' was not found.", nameof(destinationCalendarName));

        cancellationToken.ThrowIfCancellationRequested();
        var provider = _serviceFactory.Resolve(summary.Name);
        var itemResults = new List<CalendarBatchCreationItemResult>(requests.Count);

        for (var index = 0; index < requests.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var calendarEvent = await provider.AddAsync(requests[index], cancellationToken).ConfigureAwait(false);
                itemResults.Add(new CalendarBatchCreationItemResult(index, calendarEvent, null));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                itemResults.Add(new CalendarBatchCreationItemResult(
                    index,
                    null,
                    new CalendarOperationError(
                        CalendarOperationErrorCategory.Provider,
                        ProviderErrorMessage)));
            }
        }

        return new CalendarBatchCreationResult(
            summary.Name,
            summary.Type,
            itemResults.AsReadOnly());
    }
}
namespace Calendar.Core;

/// <summary>
/// Identifies an event uniquely across configured calendars.
/// </summary>
/// <param name="CalendarName">The canonical source calendar name.</param>
/// <param name="ProviderEventId">The provider event identifier.</param>
public sealed record CalendarEventIdentity(string CalendarName, string ProviderEventId);

/// <summary>
/// Associates a provider event with its source calendar and composite identity.
/// </summary>
/// <param name="SourceCalendarName">The canonical source calendar name.</param>
/// <param name="SourceCalendarType">The source calendar provider type.</param>
/// <param name="Identity">The stable cross-calendar event identity.</param>
/// <param name="Event">The provider event.</param>
public sealed record SourceCalendarEvent(
    string SourceCalendarName,
    ServiceType SourceCalendarType,
    CalendarEventIdentity Identity,
    CalendarEvent Event);

/// <summary>
/// Describes a calendar that failed during an aggregate query.
/// </summary>
/// <param name="CalendarName">The canonical calendar name.</param>
/// <param name="CalendarType">The calendar provider type.</param>
/// <param name="Error">The safe failure details.</param>
public sealed record CalendarQueryError(
    string CalendarName,
    ServiceType CalendarType,
    CalendarOperationError Error);

/// <summary>
/// Contains successful events and isolated calendar failures from an aggregate query.
/// </summary>
/// <param name="Events">The source-aware events in deterministic order.</param>
/// <param name="Errors">The per-calendar safe errors.</param>
public sealed record CalendarAggregationResult(
    IReadOnlyList<SourceCalendarEvent> Events,
    IReadOnlyList<CalendarQueryError> Errors);

/// <summary>
/// Queries events across configured calendar providers.
/// </summary>
public sealed class CalendarAggregationService
{
    private const string ConfigurationErrorMessage = "Calendar configuration is invalid.";
    private const string ProviderErrorMessage = "Calendar provider operation failed.";
    private readonly CalendarServiceFactory _serviceFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarAggregationService"/> class.
    /// </summary>
    /// <param name="serviceFactory">The calendar service factory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceFactory"/> is <see langword="null"/>.</exception>
    public CalendarAggregationService(CalendarServiceFactory serviceFactory)
    {
        ArgumentNullException.ThrowIfNull(serviceFactory);
        _serviceFactory = serviceFactory;
    }

    /// <summary>
    /// Queries all configured calendars or an explicitly selected subset concurrently.
    /// </summary>
    /// <param name="startInclusive">The inclusive range start.</param>
    /// <param name="endExclusive">The exclusive range end.</param>
    /// <param name="selectedCalendarNames">The optional configured calendar names; <see langword="null"/> selects all calendars.</param>
    /// <param name="cancellationToken">A token that cancels the entire operation.</param>
    /// <returns>The successful source-aware events and per-calendar safe errors.</returns>
    /// <exception cref="ArgumentException">The range or selected calendar names are invalid.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels the operation.</exception>
    public async Task<CalendarAggregationResult> QueryAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        IReadOnlyList<string>? selectedCalendarNames = null,
        CancellationToken cancellationToken = default)
    {
        if (startInclusive >= endExclusive)
        {
            throw new ArgumentException("The range start must be before the range end.", nameof(endExclusive));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var targets = SelectTargets(selectedCalendarNames);
        var queryTasks = targets.Select(target => QueryCalendarAsync(
            target,
            startInclusive,
            endExclusive,
            cancellationToken));
        var queryResults = await Task.WhenAll(queryTasks).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var events = queryResults
            .SelectMany(result => result.Events)
            .OrderBy(item => item.Event.StartsAt)
            .ThenBy(item => item.SourceCalendarName, StringComparer.Ordinal)
            .ThenBy(item => item.Event.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Event.Id, StringComparer.Ordinal)
            .ToArray();
        var errors = queryResults
            .Where(result => result.Error is not null)
            .Select(result => result.Error!)
            .OrderBy(error => error.CalendarName, StringComparer.Ordinal)
            .ToArray();

        return new CalendarAggregationResult(
            Array.AsReadOnly(events),
            Array.AsReadOnly(errors));
    }

    private IReadOnlyList<QueryTarget> SelectTargets(IReadOnlyList<string>? selectedCalendarNames)
    {
        var summaries = _serviceFactory.ListSummaries();
        if (selectedCalendarNames is null)
        {
            return summaries.Count > 0
                ? summaries.Select(summary => new QueryTarget(summary, summary.Name)).ToArray()
                : [new QueryTarget(new CalendarServiceSummary("Local", ServiceType.FileSystem, true), null)];
        }

        if (selectedCalendarNames.Count == 0)
        {
            throw new ArgumentException("At least one calendar must be selected.", nameof(selectedCalendarNames));
        }

        var summariesByName = summaries.ToDictionary(summary => summary.Name, StringComparer.OrdinalIgnoreCase);
        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new List<QueryTarget>(selectedCalendarNames.Count);

        foreach (var selectedName in selectedCalendarNames)
        {
            if (string.IsNullOrWhiteSpace(selectedName))
            {
                throw new ArgumentException("Selected calendar names cannot be blank.", nameof(selectedCalendarNames));
            }

            var normalizedName = selectedName.Trim();
            if (!selectedNames.Add(normalizedName))
            {
                throw new ArgumentException($"Calendar '{normalizedName}' is selected more than once.", nameof(selectedCalendarNames));
            }

            if (!summariesByName.TryGetValue(normalizedName, out var summary))
            {
                throw new ArgumentException($"Calendar '{normalizedName}' was not found.", nameof(selectedCalendarNames));
            }

            targets.Add(new QueryTarget(summary, summary.Name));
        }

        return targets;
    }

    private async Task<CalendarQueryResult> QueryCalendarAsync(
        QueryTarget target,
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = _serviceFactory.Resolve(target.ResolveName);
            var providerEvents = await provider.ListBetweenAsync(
                startInclusive,
                endExclusive,
                cancellationToken).ConfigureAwait(false);
            var events = providerEvents.Select(calendarEvent => new SourceCalendarEvent(
                target.Summary.Name,
                target.Summary.Type,
                new CalendarEventIdentity(target.Summary.Name, calendarEvent.Id),
                calendarEvent));
            return new CalendarQueryResult(events.ToArray(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CalendarServiceConfigurationException)
        {
            return FailedResult(target, CalendarOperationErrorCategory.Configuration, ConfigurationErrorMessage);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FailedResult(target, CalendarOperationErrorCategory.Provider, ProviderErrorMessage);
        }
    }

    private static CalendarQueryResult FailedResult(
        QueryTarget target,
        CalendarOperationErrorCategory category,
        string message) =>
        new(
            [],
            new CalendarQueryError(
                target.Summary.Name,
                target.Summary.Type,
                new CalendarOperationError(category, message)));

    private sealed record QueryTarget(CalendarServiceSummary Summary, string? ResolveName);

    private sealed record CalendarQueryResult(
        IReadOnlyList<SourceCalendarEvent> Events,
        CalendarQueryError? Error);
}
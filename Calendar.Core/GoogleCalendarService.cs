using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace Calendar.Core;

/// <summary>
/// Provides event operations against the primary Google Calendar.
/// </summary>
public sealed class GoogleCalendarService : ICalendarService
{
    private readonly string _secretsJsonPath;
    private CalendarService? _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleCalendarService"/> class.
    /// </summary>
    /// <param name="secretsJsonPath">The Google OAuth client secrets JSON path.</param>
    /// <exception cref="ArgumentException"><paramref name="secretsJsonPath"/> is blank.</exception>
    public GoogleCalendarService(string secretsJsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsJsonPath);
        _secretsJsonPath = secretsJsonPath;
    }

    /// <inheritdoc/>
    public async Task<CalendarEvent> AddAsync(
        CalendarEventCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var eventId = Guid.NewGuid().ToString("N").ToLowerInvariant();
        var googleEvent = GoogleCalendarEventMapper.ToGoogleEvent(eventId, request);
        var createdEvent = await _service!.Events
            .Insert(googleEvent, "primary")
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return GoogleCalendarEventMapper.ToCalendarEvent(createdEvent);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CalendarEvent>> ListBetweenAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var request = _service!.Events.List("primary");
        request.TimeMinDateTimeOffset = startInclusive;
        request.TimeMaxDateTimeOffset = endExclusive;
        request.SingleEvents = true;

        var feed = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return (feed.Items ?? [])
            .Select(GoogleCalendarEventMapper.ToCalendarEvent)
            .Where(calendarEvent => calendarEvent.StartsAt < endExclusive && calendarEvent.EndsAt > startInclusive)
            .OrderBy(calendarEvent => calendarEvent.StartsAt)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, CalendarEvent? DeletedEvent)> DeleteAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var item = await _service!.Events
                .Get("primary", eventId)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            var deletedEvent = GoogleCalendarEventMapper.ToCalendarEvent(item);

            await _service.Events
                .Delete("primary", eventId)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            return (true, deletedEvent);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (false, null);
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_service is not null)
        {
            return;
        }

        if (!File.Exists(_secretsJsonPath))
        {
            throw new FileNotFoundException($"Google client secrets file not found: {_secretsJsonPath}");
        }

        await using var stream = new FileStream(
            _secretsJsonPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        var credentialPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "calendar-cli",
            "google-tokens");
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            [CalendarService.Scope.Calendar],
            "user",
            cancellationToken,
            new FileDataStore(credentialPath, true)).ConfigureAwait(false);

        _service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Calendar CLI"
        });
    }
}
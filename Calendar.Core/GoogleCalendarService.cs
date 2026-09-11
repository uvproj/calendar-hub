using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Security.Cryptography;
using System.Text;

namespace Calendar.Core;

/// <summary>
/// Provides event operations against a configured Google Calendar.
/// </summary>
public sealed class GoogleCalendarService : ICalendarService
{
    private readonly string _serviceIdentity;
    private readonly string _secretsJsonPath;
    private readonly string _calendarId;
    private CalendarService? _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleCalendarService"/> class.
    /// </summary>
    /// <param name="serviceIdentity">The stable configured service identity used to isolate OAuth tokens.</param>
    /// <param name="secretsJsonPath">The Google OAuth client secrets JSON path.</param>
    /// <param name="calendarId">The optional Google calendar identifier. The default is <c>primary</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="serviceIdentity"/> or <paramref name="secretsJsonPath"/> is blank.</exception>
    public GoogleCalendarService(
        string serviceIdentity,
        string secretsJsonPath,
        string? calendarId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsJsonPath);

        _serviceIdentity = serviceIdentity.Trim();
        _secretsJsonPath = secretsJsonPath;
        _calendarId = string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId.Trim();
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
            .Insert(googleEvent, _calendarId)
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

        var request = _service!.Events.List(_calendarId);
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
                .Get(_calendarId, eventId)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            var deletedEvent = GoogleCalendarEventMapper.ToCalendarEvent(item);

            await _service.Events
                .Delete(_calendarId, eventId)
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
        var tokenRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "calendar-cli",
            "google-tokens");
        var credentialPath = GetTokenCacheDirectory(_serviceIdentity, tokenRoot);
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

    internal static string GetTokenCacheDirectory(string serviceIdentity, string tokenRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenRoot);

        var normalizedIdentity = serviceIdentity.Trim().ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedIdentity));
        var safeIdentity = Convert.ToHexStringLower(hash);
        return Path.Combine(Path.GetFullPath(tokenRoot), safeIdentity);
    }
}
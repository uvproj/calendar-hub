using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace CalendarCli;

internal sealed class GoogleCalendarService : ICalendarService
{
    private readonly string _secretsJsonPath;
    private CalendarService? _service;

    public GoogleCalendarService(string secretsJsonPath)
    {
        _secretsJsonPath = secretsJsonPath;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_service != null) return;

        if (!File.Exists(_secretsJsonPath))
        {
            throw new FileNotFoundException($"Google client secrets file not found: {_secretsJsonPath}");
        }

        UserCredential credential;
        using (var stream = new FileStream(_secretsJsonPath, FileMode.Open, FileAccess.Read))
        {
            var credPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "calendar-cli",
                "google-tokens");

            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                [CalendarService.Scope.Calendar],
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true));
        }

        _service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Calendar CLI",
        });
    }

    public async Task AddAsync(CalendarEvent calendarEvent)
    {
        await EnsureInitializedAsync();

        var googleEvent = new Event
        {
            Id = calendarEvent.Id,
            Summary = calendarEvent.Name,
            Description = calendarEvent.Description,
            Location = calendarEvent.Location,
            Start = new EventDateTime
            {
                DateTimeDateTimeOffset = calendarEvent.StartsAt,
                TimeZone = "UTC"
            },
            End = new EventDateTime
            {
                DateTimeDateTimeOffset = calendarEvent.StartsAt.AddHours(1),
                TimeZone = "UTC"
            },
            Attendees = calendarEvent.Invitees.Select(email => new EventAttendee { Email = email }).ToList()
        };

        await _service!.Events.Insert(googleEvent, "primary").ExecuteAsync();
    }

    public async Task<IReadOnlyList<CalendarEvent>> ListBetweenAsync(DateTimeOffset startInclusive, DateTimeOffset endExclusive)
    {
        await EnsureInitializedAsync();

        var request = _service!.Events.List("primary");
        request.TimeMinDateTimeOffset = startInclusive;
        request.TimeMaxDateTimeOffset = endExclusive;
        request.SingleEvents = true;

        var feed = await request.ExecuteAsync();

        var results = new List<CalendarEvent>();
        foreach (var item in feed.Items ?? Enumerable.Empty<Event>())
        {
            var start = item.Start?.DateTimeDateTimeOffset ?? DateTimeOffset.MinValue;
            var invitees = item.Attendees?.Select(a => a.Email).Where(e => !string.IsNullOrEmpty(e)).ToList() ?? new List<string>();

            results.Add(new CalendarEvent(
                item.Id,
                item.Summary ?? "Untitled Event",
                item.Description,
                item.Location,
                start,
                invitees
            ));
        }

        return results.OrderBy(e => e.StartsAt).ToList();
    }

    public async Task<(bool Succeeded, CalendarEvent? DeletedEvent)> DeleteAsync(Guid eventId)
    {
        await EnsureInitializedAsync();
        var stringId = eventId.ToString("n").ToLowerInvariant();

        try
        {
            var item = await _service!.Events.Get("primary", stringId).ExecuteAsync();
            var start = item.Start?.DateTimeDateTimeOffset ?? DateTimeOffset.MinValue;
            var invitees = item.Attendees?.Select(a => a.Email).Where(e => !string.IsNullOrEmpty(e)).ToList() ?? new List<string>();

            var deletedEvent = new CalendarEvent(
                eventId,
                item.Summary ?? "Untitled Event",
                item.Description,
                item.Location,
                start,
                invitees
            );

            await _service.Events.Delete("primary", stringId).ExecuteAsync();
            return (true, deletedEvent);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (false, null);
        }
    }
}

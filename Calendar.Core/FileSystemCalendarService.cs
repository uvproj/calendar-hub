using System.Text.Json;

namespace Calendar.Core;

/// <summary>
/// Persists calendar events in a local JSON file.
/// </summary>
public sealed class FileSystemCalendarService : ICalendarService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemCalendarService"/> class.
    /// </summary>
    /// <param name="filePath">The JSON persistence file path.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is blank.</exception>
    public FileSystemCalendarService(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc/>
    public async Task<CalendarEvent> AddAsync(
        CalendarEventCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var events = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        var calendarEvent = new CalendarEvent(
            Guid.NewGuid().ToString("N").ToLowerInvariant(),
            request.Name,
            request.Description,
            request.Location,
            request.StartsAt,
            request.EndsAt,
            request.IsAllDay,
            request.Invitees.ToList());

        events.Add(calendarEvent);
        await SaveAllAsync(events, cancellationToken).ConfigureAwait(false);
        return calendarEvent;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CalendarEvent>> ListBetweenAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        var events = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return events
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

        var events = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        var deletedEvent = events.FirstOrDefault(calendarEvent => calendarEvent.Id == eventId);

        if (deletedEvent is null)
        {
            return (false, null);
        }

        events.RemoveAll(calendarEvent => calendarEvent.Id == eventId);
        await SaveAllAsync(events, cancellationToken).ConfigureAwait(false);
        return (true, deletedEvent);
    }

    private async Task<List<CalendarEvent>> LoadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var storedEvents = JsonSerializer.Deserialize<List<StoredCalendarEvent>>(json, SerializerOptions) ?? [];
            return storedEvents.Select(ToCalendarEvent).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SaveAllAsync(List<CalendarEvent> events, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var storedEvents = events
            .OrderBy(calendarEvent => calendarEvent.StartsAt)
            .Select(StoredCalendarEvent.FromCalendarEvent)
            .ToList();
        var json = JsonSerializer.Serialize(storedEvents, SerializerOptions);

        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }

    private static CalendarEvent ToCalendarEvent(StoredCalendarEvent storedEvent)
    {
        var endsAt = storedEvent.EndsAt ?? storedEvent.StartsAt.AddHours(1);
        return new CalendarEvent(
            storedEvent.Id,
            storedEvent.Name,
            storedEvent.Description,
            storedEvent.Location,
            storedEvent.StartsAt,
            endsAt,
            storedEvent.IsAllDay,
            storedEvent.Invitees ?? []);
    }

    private sealed record StoredCalendarEvent(
        string Id,
        string Name,
        string? Description,
        string? Location,
        DateTimeOffset StartsAt,
        DateTimeOffset? EndsAt,
        bool IsAllDay,
        List<string>? Invitees)
    {
        internal static StoredCalendarEvent FromCalendarEvent(CalendarEvent calendarEvent)
        {
            return new StoredCalendarEvent(
                calendarEvent.Id,
                calendarEvent.Name,
                calendarEvent.Description,
                calendarEvent.Location,
                calendarEvent.StartsAt,
                calendarEvent.EndsAt,
                calendarEvent.IsAllDay,
                calendarEvent.Invitees.ToList());
        }
    }
}
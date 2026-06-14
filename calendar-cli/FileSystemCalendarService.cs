using System.Text.Json;

namespace CalendarCli;

internal sealed class FileSystemCalendarService(string filePath) : ICalendarService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath = filePath;

    public async Task AddAsync(CalendarEvent calendarEvent)
    {
        var events = await LoadAllAsync();
        events.Add(calendarEvent);
        await SaveAllAsync(events);
    }

    public async Task<IReadOnlyList<CalendarEvent>> ListBetweenAsync(DateTimeOffset startInclusive, DateTimeOffset endExclusive)
    {
        var events = await LoadAllAsync();
        return events
            .Where(calendarEvent => calendarEvent.StartsAt >= startInclusive && calendarEvent.StartsAt < endExclusive)
            .OrderBy(calendarEvent => calendarEvent.StartsAt)
            .ToList();
    }

    public async Task<(bool Succeeded, CalendarEvent? DeletedEvent)> DeleteAsync(string eventId)
    {
        var events = await LoadAllAsync();
        var deletedEvent = events.FirstOrDefault(calendarEvent => calendarEvent.Id == eventId);

        if (deletedEvent is null)
        {
            return (false, null);
        }

        events.RemoveAll(calendarEvent => calendarEvent.Id == eventId);
        await SaveAllAsync(events);
        return (true, deletedEvent);
    }

    private async Task<List<CalendarEvent>> LoadAllAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<CalendarEvent>>(json, SerializerOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task SaveAllAsync(List<CalendarEvent> events)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(
            events.OrderBy(calendarEvent => calendarEvent.StartsAt).ToList(),
            SerializerOptions);

        await File.WriteAllTextAsync(_filePath, json);
    }
}

using System.Text.Json;

namespace Calendar.Core.Tests;

public sealed class FileSystemCalendarServiceTests
{
    [Fact]
    public async Task AddAsync_TimedRequest_GeneratesLowercaseGuidNId()
    {
        var filePath = CreateFilePath();
        var service = new FileSystemCalendarService(filePath);
        var request = CreateTimedRequest("Planning", Utc(2026, 9, 10, 9));

        var createdEvent = await service.AddAsync(request);

        Assert.Matches("^[0-9a-f]{32}$", createdEvent.Id);
    }

    [Fact]
    public async Task AddAsync_TimedRequest_PersistsExpandedShape()
    {
        var filePath = CreateFilePath();
        var service = new FileSystemCalendarService(filePath);
        var startsAt = Utc(2026, 9, 10, 9);

        await service.AddAsync(CreateTimedRequest("Planning", startsAt));

        var json = await File.ReadAllTextAsync(filePath);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(startsAt.AddHours(1), document.RootElement[0].GetProperty("EndsAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task ListBetweenAsync_OldJson_DefaultsOneHourTimedEvent()
    {
        var filePath = CreateFilePath();
        var startsAt = Utc(2026, 9, 10, 9);
        await File.WriteAllTextAsync(filePath, OldEventJson("old-id", startsAt));
        var service = new FileSystemCalendarService(filePath);

        var events = await service.ListBetweenAsync(startsAt, startsAt.AddHours(2));

        Assert.Equal(startsAt.AddHours(1), Assert.Single(events).EndsAt);
    }

    [Fact]
    public async Task AddAsync_AfterReadingOldJson_RewritesExpandedShape()
    {
        var filePath = CreateFilePath();
        var startsAt = Utc(2026, 9, 10, 9);
        await File.WriteAllTextAsync(filePath, OldEventJson("old-id", startsAt));
        var service = new FileSystemCalendarService(filePath);

        await service.AddAsync(CreateTimedRequest("New event", startsAt.AddDays(1)));

        var json = await File.ReadAllTextAsync(filePath);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement[0].TryGetProperty("EndsAt", out _));
    }

    [Fact]
    public async Task AddAsync_AllDayRequest_PersistsExclusiveDateBoundaries()
    {
        var filePath = CreateFilePath();
        var service = new FileSystemCalendarService(filePath);
        var startsAt = Utc(2026, 9, 10);
        var request = new CalendarEventCreateRequest(
            "Conference",
            null,
            null,
            startsAt,
            startsAt.AddDays(2),
            true,
            []);

        await service.AddAsync(request);
        var events = await service.ListBetweenAsync(startsAt, startsAt.AddDays(3));

        Assert.True(Assert.Single(events).IsAllDay);
    }

    [Fact]
    public async Task ListBetweenAsync_EventStartsBeforeRangeAndEndsInsideRange_IncludesEvent()
    {
        var filePath = CreateFilePath();
        var service = new FileSystemCalendarService(filePath);
        var startsAt = Utc(2026, 9, 10, 9);
        await service.AddAsync(CreateTimedRequest("Overlap", startsAt));

        var events = await service.ListBetweenAsync(startsAt.AddMinutes(30), startsAt.AddHours(2));

        Assert.Single(events);
    }

    [Fact]
    public async Task ListBetweenAsync_EventEndsAtRangeStart_ExcludesEvent()
    {
        var filePath = CreateFilePath();
        var service = new FileSystemCalendarService(filePath);
        var startsAt = Utc(2026, 9, 10, 9);
        await service.AddAsync(CreateTimedRequest("Adjacent", startsAt));

        var events = await service.ListBetweenAsync(startsAt.AddHours(1), startsAt.AddHours(2));

        Assert.Empty(events);
    }

    [Fact]
    public async Task ListBetweenAsync_UnorderedEvents_ReturnsStartTimeOrder()
    {
        var filePath = CreateFilePath();
        var service = new FileSystemCalendarService(filePath);
        var startsAt = Utc(2026, 9, 10, 9);
        await service.AddAsync(CreateTimedRequest("Later", startsAt.AddHours(2)));
        await service.AddAsync(CreateTimedRequest("Earlier", startsAt));

        var events = await service.ListBetweenAsync(startsAt, startsAt.AddHours(4));

        Assert.Equal(["Earlier", "Later"], events.Select(calendarEvent => calendarEvent.Name));
    }

    private static CalendarEventCreateRequest CreateTimedRequest(string name, DateTimeOffset startsAt)
    {
        return new CalendarEventCreateRequest(name, null, null, startsAt, startsAt.AddHours(1), false, []);
    }

    private static string CreateFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"calendar-core-{Guid.NewGuid():N}.json");
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0)
    {
        return new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero);
    }

    private static string OldEventJson(string id, DateTimeOffset startsAt)
    {
        return $$"""
            [
              {
                "Id": "{{id}}",
                "Name": "Legacy",
                "Description": null,
                "Location": null,
                "StartsAt": "{{startsAt:O}}",
                "Invitees": []
              }
            ]
            """;
    }
}
namespace CalendarCli;

internal sealed record CalendarEvent(
    string Id,
    string Name,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    List<string> Invitees);

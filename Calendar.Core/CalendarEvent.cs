namespace Calendar.Core;

/// <summary>
/// Represents a calendar event returned by a calendar provider.
/// </summary>
public sealed record CalendarEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEvent"/> class.
    /// </summary>
    /// <param name="id">The provider event identifier.</param>
    /// <param name="name">The event name.</param>
    /// <param name="description">The optional event description.</param>
    /// <param name="location">The optional event location.</param>
    /// <param name="startsAt">The inclusive event start.</param>
    /// <param name="endsAt">The exclusive event end.</param>
    /// <param name="isAllDay"><see langword="true"/> for an all-day event; otherwise, <see langword="false"/>.</param>
    /// <param name="invitees">The invited email addresses.</param>
    /// <exception cref="ArgumentException">An identifier or name is blank, the interval is invalid, or all-day boundaries are not midnight.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="invitees"/> is <see langword="null"/>.</exception>
    public CalendarEvent(
        string id,
        string name,
        string? description,
        string? location,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isAllDay,
        List<string> invitees)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(invitees);
        CalendarEventValidation.ValidateInterval(startsAt, endsAt, isAllDay);

        Id = id;
        Name = name;
        Description = description;
        Location = location;
        StartsAt = startsAt;
        EndsAt = endsAt;
        IsAllDay = isAllDay;
        Invitees = invitees.ToList().AsReadOnly();
    }

    /// <summary>Gets the provider event identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the event name.</summary>
    public string Name { get; }

    /// <summary>Gets the optional event description.</summary>
    public string? Description { get; }

    /// <summary>Gets the optional event location.</summary>
    public string? Location { get; }

    /// <summary>Gets the inclusive event start.</summary>
    public DateTimeOffset StartsAt { get; }

    /// <summary>Gets the exclusive event end.</summary>
    public DateTimeOffset EndsAt { get; }

    /// <summary>Gets a value that indicates whether the event spans whole dates.</summary>
    public bool IsAllDay { get; }

    /// <summary>Gets the invited email addresses.</summary>
    public IReadOnlyList<string> Invitees { get; }
}
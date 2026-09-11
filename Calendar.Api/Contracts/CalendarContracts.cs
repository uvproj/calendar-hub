using Calendar.Core;

namespace Calendar.Api.Contracts;

/// <summary>Describes a configured calendar without provider secrets or paths.</summary>
public sealed record CalendarSummaryResponse(string Name, string Type, bool IsDefault)
{
    internal static CalendarSummaryResponse FromCore(CalendarServiceSummary summary) =>
        new(summary.Name, summary.Type.ToString(), summary.IsDefault);
}

/// <summary>Identifies an event across configured calendars.</summary>
public sealed record CalendarEventIdentityResponse(string CalendarName, string ProviderEventId);

/// <summary>Describes an event and its source calendar.</summary>
public sealed record CalendarEventResponse(
    string SourceCalendarName,
    string SourceCalendarType,
    CalendarEventIdentityResponse Identity,
    string Name,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    IReadOnlyList<string> Invitees)
{
    internal static CalendarEventResponse FromCore(SourceCalendarEvent sourceEvent) =>
        new(
            sourceEvent.SourceCalendarName,
            sourceEvent.SourceCalendarType.ToString(),
            new CalendarEventIdentityResponse(
                sourceEvent.Identity.CalendarName,
                sourceEvent.Identity.ProviderEventId),
            sourceEvent.Event.Name,
            sourceEvent.Event.Description,
            sourceEvent.Event.Location,
            sourceEvent.Event.StartsAt,
            sourceEvent.Event.EndsAt,
            sourceEvent.Event.IsAllDay,
            sourceEvent.Event.Invitees);
}

/// <summary>Describes one safe calendar query failure.</summary>
public sealed record CalendarQueryErrorResponse(
    string CalendarName,
    string CalendarType,
    string Category,
    string Message)
{
    internal static CalendarQueryErrorResponse FromCore(CalendarQueryError error) =>
        new(error.CalendarName, error.CalendarType.ToString(), error.Error.Category.ToString(), error.Error.Message);
}

/// <summary>Contains aggregate calendar events and isolated provider failures.</summary>
public sealed record AggregateEventsResponse(
    IReadOnlyList<CalendarEventResponse> Events,
    IReadOnlyList<CalendarQueryErrorResponse> Errors)
{
    internal static AggregateEventsResponse FromCore(CalendarAggregationResult result) =>
        new(
            result.Events.Select(CalendarEventResponse.FromCore).ToArray(),
            result.Errors.Select(CalendarQueryErrorResponse.FromCore).ToArray());
}

/// <summary>Supplies a structured event draft for confirmed execution.</summary>
public sealed record CalendarEventDraftRequest(
    string Name,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    IReadOnlyList<string> Invitees);

/// <summary>Supplies a confirmed batch and its explicit destination calendar.</summary>
public sealed record ConfirmBatchCreationRequest(
    string DestinationCalendar,
    IReadOnlyList<CalendarEventDraftRequest> Events);

/// <summary>Describes one ordered batch item outcome.</summary>
public sealed record BatchCreationItemResponse(
    int Index,
    CalendarEventResponse? Event,
    string? ErrorCategory,
    string? ErrorMessage);

/// <summary>Contains ordered outcomes from a confirmed event batch.</summary>
public sealed record BatchCreationResponse(
    string DestinationCalendarName,
    string DestinationCalendarType,
    IReadOnlyList<BatchCreationItemResponse> Items)
{
    internal static BatchCreationResponse FromCore(CalendarBatchCreationResult result) =>
        new(
            result.DestinationCalendarName,
            result.DestinationCalendarType.ToString(),
            result.Items.Select(item => new BatchCreationItemResponse(
                item.Index,
                item.Event is null
                    ? null
                    : CalendarEventResponse.FromCore(new SourceCalendarEvent(
                        result.DestinationCalendarName,
                        result.DestinationCalendarType,
                        new CalendarEventIdentity(result.DestinationCalendarName, item.Event.Id),
                        item.Event)),
                item.Error?.Category.ToString(),
                item.Error?.Message)).ToArray());
}
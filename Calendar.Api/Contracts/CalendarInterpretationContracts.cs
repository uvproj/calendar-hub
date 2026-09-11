using Calendar.Api.Ai;
using Calendar.Core;

namespace Calendar.Api.Contracts;

/// <summary>Supplies free-form calendar text and one explicit destination calendar ID.</summary>
public sealed record CreateCalendarInterpretationRequest(string Text, string DestinationCalendar);

/// <summary>Describes an editable event draft returned by calendar text interpretation.</summary>
public sealed record CalendarInterpretationDraftResponse(
    string Name,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    IReadOnlyList<string> Invitees)
{
    internal static CalendarInterpretationDraftResponse FromCore(CalendarEventCreateRequest draft) =>
        new(
            draft.Name,
            draft.Description,
            draft.Location,
            draft.StartsAt,
            draft.EndsAt,
            draft.IsAllDay,
            draft.Invitees);
}

/// <summary>Contains an editable interpretation preview that has not changed any calendar.</summary>
public sealed record CalendarInterpretationResponse(
    string DestinationCalendar,
    IReadOnlyList<CalendarInterpretationDraftResponse> Drafts,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> ClarificationErrors)
{
    internal static CalendarInterpretationResponse FromResult(
        string destinationCalendar,
        CalendarTextInterpretationResult result) =>
        new(
            destinationCalendar,
            result.Drafts.Select(CalendarInterpretationDraftResponse.FromCore).ToArray(),
            result.Warnings,
            result.ClarificationErrors);
}

/// <summary>Describes a safe calendar interpretation failure.</summary>
public sealed record CalendarInterpretationFailureResponse(string Code, string Message);
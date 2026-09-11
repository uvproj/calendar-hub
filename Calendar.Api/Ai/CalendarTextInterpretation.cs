using Calendar.Core;

namespace Calendar.Api.Ai;

/// <summary>
/// Supplies the context required to interpret calendar text without allowing a provider to choose a destination.
/// </summary>
/// <param name="Text">The free-form event description.</param>
/// <param name="DestinationCalendar">The explicitly selected destination calendar.</param>
/// <param name="TimeZone">The configured application time zone.</param>
/// <param name="CurrentInstant">The current instant used to resolve relative dates.</param>
public sealed record CalendarTextInterpretationInput(
    string Text,
    string DestinationCalendar,
    TimeZoneInfo TimeZone,
    DateTimeOffset CurrentInstant);

/// <summary>
/// Describes a safe interpretation failure without provider details or model content.
/// </summary>
/// <param name="Code">The stable failure category.</param>
/// <param name="Message">The client-safe failure message.</param>
public sealed record CalendarTextInterpretationFailure(string Code, string Message);

/// <summary>
/// Contains ordered event drafts and model-supplied guidance for an interpretation preview.
/// </summary>
/// <param name="Drafts">The validated event drafts in source order.</param>
/// <param name="Warnings">The safe warnings associated with the drafts.</param>
/// <param name="ClarificationErrors">The questions or missing details that prevent a complete interpretation.</param>
/// <param name="Failure">The safe provider or output failure, when interpretation could not complete.</param>
public sealed record CalendarTextInterpretationResult(
    IReadOnlyList<CalendarEventCreateRequest> Drafts,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> ClarificationErrors,
    CalendarTextInterpretationFailure? Failure = null);

/// <summary>
/// Interprets free-form text as structured calendar event drafts without creating events.
/// </summary>
public interface ICalendarTextInterpreter
{
    /// <summary>
    /// Interprets text using the supplied destination, time zone, and current-time context.
    /// </summary>
    /// <param name="input">The provider-neutral interpretation input.</param>
    /// <param name="cancellationToken">A token that cancels the interpretation request.</param>
    /// <returns>The validated preview or a safe typed failure.</returns>
    Task<CalendarTextInterpretationResult> InterpretAsync(
        CalendarTextInterpretationInput input,
        CancellationToken cancellationToken);
}
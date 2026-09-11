using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using Calendar.Core;

namespace Calendar.Api.Ai;

/// <summary>
/// Parses and validates the strict provider response used for calendar interpretation.
/// </summary>
public sealed class CalendarTextInterpretationParser
{
    private const int MaxGuidanceItems = 50;
    private const int MaxGuidanceLength = 500;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>
    /// Parses strict JSON and converts only fully validated drafts to core creation requests.
    /// </summary>
    /// <param name="json">The provider JSON response.</param>
    /// <param name="currentInstant">The current instant used to enforce the supported date window.</param>
    /// <returns>The validated interpretation or a safe output-validation failure.</returns>
    public CalendarTextInterpretationResult Parse(string json, DateTimeOffset currentInstant)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 131_072)
        {
            return InvalidOutput();
        }

        CalendarInterpretationWireResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<CalendarInterpretationWireResponse>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return InvalidOutput();
        }

        if (response?.Drafts is null ||
            response.Warnings is null ||
            response.ClarificationErrors is null ||
            response.Drafts.Count > CalendarBatchCreationService.MaxBatchSize ||
            !IsValidGuidance(response.Warnings) ||
            !IsValidGuidance(response.ClarificationErrors) ||
            (response.Drafts.Count == 0 && response.ClarificationErrors.Count == 0))
        {
            return InvalidOutput();
        }

        var earliest = currentInstant.AddYears(-2);
        var latest = currentInstant.AddYears(2);
        var drafts = new List<CalendarEventCreateRequest>(response.Drafts.Count);
        foreach (var draft in response.Drafts)
        {
            if (!TryConvertDraft(draft, earliest, latest, out var converted))
            {
                return InvalidOutput();
            }

            drafts.Add(converted);
        }

        return new CalendarTextInterpretationResult(
            drafts.AsReadOnly(),
            response.Warnings.Select(item => item!.Trim()).ToArray(),
            response.ClarificationErrors.Select(item => item!.Trim()).ToArray());
    }

    private static bool TryConvertDraft(
        CalendarEventDraftWire? draft,
        DateTimeOffset earliest,
        DateTimeOffset latest,
        out CalendarEventCreateRequest converted)
    {
        converted = null!;
        if (draft is null ||
            string.IsNullOrWhiteSpace(draft.Name) ||
            draft.Name.Trim().Length > 200 ||
            draft.Description?.Length > 4000 ||
            draft.Location?.Length > 500 ||
            draft.Invitees is null ||
            draft.Invitees.Count > 100 ||
            draft.StartsAt < earliest ||
            draft.EndsAt > latest ||
            draft.EndsAt <= draft.StartsAt ||
            (draft.IsAllDay &&
                (draft.StartsAt.TimeOfDay != TimeSpan.Zero || draft.EndsAt.TimeOfDay != TimeSpan.Zero)) ||
            draft.Invitees.Any(invitee => !IsValidEmail(invitee)))
        {
            return false;
        }

        try
        {
            converted = new CalendarEventCreateRequest(
                draft.Name.Trim(),
                TrimToNull(draft.Description),
                TrimToNull(draft.Location),
                draft.StartsAt,
                draft.EndsAt,
                draft.IsAllDay,
                draft.Invitees.Select(invitee => invitee!.Trim()).ToList());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254)
        {
            return false;
        }

        var trimmed = value.Trim();
        return MailAddress.TryCreate(trimmed, out var address) &&
            string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidGuidance(IReadOnlyList<string?> items) =>
        items.Count <= MaxGuidanceItems &&
        items.All(item => !string.IsNullOrWhiteSpace(item) && item.Length <= MaxGuidanceLength);

    private static CalendarTextInterpretationResult InvalidOutput() =>
        new(
            [],
            [],
            [],
            new CalendarTextInterpretationFailure(
                "invalid_model_output",
                "The calendar text could not be converted to a valid event preview."));

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CalendarInterpretationWireResponse(
        IReadOnlyList<CalendarEventDraftWire?>? Drafts,
        IReadOnlyList<string?>? Warnings,
        IReadOnlyList<string?>? ClarificationErrors);

    private sealed record CalendarEventDraftWire(
        string? Name,
        string? Description,
        string? Location,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        bool IsAllDay,
        IReadOnlyList<string?>? Invitees);
}
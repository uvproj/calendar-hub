using System.Text.Json;
using Calendar.Api.Ai;

namespace Calendar.Api.Tests;

public sealed class CalendarTextInterpretationParserTests
{
    private static readonly DateTimeOffset CurrentInstant =
        new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_MultipleValidDrafts_ReturnsOrderedDrafts()
    {
        var parser = new CalendarTextInterpretationParser();
        var json = Serialize(Draft(), Draft(
            name: "Holiday",
            startsAt: "2026-09-20T00:00:00+00:00",
            endsAt: "2026-09-22T00:00:00+00:00",
            isAllDay: true));

        var result = parser.Parse(json, CurrentInstant);

        Assert.Equal(["Dinner", "Holiday"], result.Drafts.Select(draft => draft.Name));
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsSafeFailure()
    {
        var parser = new CalendarTextInterpretationParser();

        var result = parser.Parse("{not-json", CurrentInstant);

        Assert.Equal("invalid_model_output", result.Failure?.Code);
    }

    [Fact]
    public void Parse_UnknownField_ReturnsSafeFailure()
    {
        var parser = new CalendarTextInterpretationParser();
        const string json = """
            {"drafts":[],"warnings":[],"clarificationErrors":["Which day?"],"operation":"create"}
            """;

        var result = parser.Parse(json, CurrentInstant);

        Assert.Equal("invalid_model_output", result.Failure?.Code);
    }

    [Theory]
    [InlineData("2026-09-12T18:00:00+00:00", "2026-09-12T18:00:00+00:00", false, "family@example.com")]
    [InlineData("2026-09-12T01:00:00+00:00", "2026-09-13T00:00:00+00:00", true, "family@example.com")]
    [InlineData("2029-09-12T18:00:00+00:00", "2029-09-12T19:00:00+00:00", false, "family@example.com")]
    [InlineData("2026-09-12T18:00:00+00:00", "2026-09-12T19:00:00+00:00", false, "Family Member <family@example.com>")]
    public void Parse_InvalidIntervalBoundaryWindowOrEmail_ReturnsSafeFailure(
        string startsAt,
        string endsAt,
        bool isAllDay,
        string invitee)
    {
        var parser = new CalendarTextInterpretationParser();
        var json = Serialize(Draft(
            startsAt: startsAt,
            endsAt: endsAt,
            isAllDay: isAllDay,
            invitees: [invitee]));

        var result = parser.Parse(json, CurrentInstant);

        Assert.Equal("invalid_model_output", result.Failure?.Code);
    }

    [Fact]
    public void Parse_TooManyDrafts_ReturnsSafeFailure()
    {
        var parser = new CalendarTextInterpretationParser();
        var drafts = Enumerable.Range(0, 51).Select(index => Draft(name: $"Event {index}")).ToArray();

        var result = parser.Parse(Serialize(drafts), CurrentInstant);

        Assert.Equal("invalid_model_output", result.Failure?.Code);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Dinner", "description")]
    public void Parse_InvalidNameOrLength_ReturnsSafeFailure(string name, string? oversizedField)
    {
        var parser = new CalendarTextInterpretationParser();
        var draft = new
        {
            name,
            description = oversizedField is null ? null : new string('x', 4001),
            location = (string?)null,
            startsAt = "2026-09-12T18:00:00+00:00",
            endsAt = "2026-09-12T19:00:00+00:00",
            isAllDay = false,
            invitees = Array.Empty<string>()
        };

        var result = parser.Parse(Serialize(draft), CurrentInstant);

        Assert.Equal("invalid_model_output", result.Failure?.Code);
    }

    private static string Serialize(params object[] drafts) => JsonSerializer.Serialize(new
    {
        drafts,
        warnings = Array.Empty<string>(),
        clarificationErrors = Array.Empty<string>()
    });

    private static object Draft(
        string name = "Dinner",
        string startsAt = "2026-09-12T18:00:00+00:00",
        string endsAt = "2026-09-12T19:00:00+00:00",
        bool isAllDay = false,
        string[]? invitees = null) => new
        {
            name,
            description = (string?)null,
            location = "Home",
            startsAt,
            endsAt,
            isAllDay,
            invitees = invitees ?? ["family@example.com"]
        };
}
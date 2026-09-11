namespace Calendar.Api.Ai;

internal sealed class UnavailableCalendarTextInterpreter : ICalendarTextInterpreter
{
    private readonly string _code;

    internal UnavailableCalendarTextInterpreter(string code)
    {
        _code = code;
    }

    public Task<CalendarTextInterpretationResult> InterpretAsync(
        CalendarTextInterpretationInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CalendarTextInterpretationResult(
            [],
            [],
            [],
            new CalendarTextInterpretationFailure(
                _code,
                "Calendar text interpretation is not available.")));
    }
}
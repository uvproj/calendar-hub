using Calendar.Api.Ai;
using Calendar.Api.Contracts;
using Calendar.Api.Services;

namespace Calendar.Api.Endpoints;

internal static class CalendarInterpretationEndpoints
{
    internal const long MaxRequestBodySize = 16_384;
    internal const int MaxTextLength = 4000;

    internal static WebApplication MapCalendarInterpretationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/calendar-interpretations", InterpretAsync)
            .RequireAuthorization()
            .RequireAntiforgery()
            .RequireRateLimiting("calendar-interpretation")
            .WithSummary("Previews events interpreted from calendar text")
            .WithDescription("Returns editable structured drafts for one explicit destination calendar ID without creating events.")
            .Produces<CalendarInterpretationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<CalendarInterpretationFailureResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<CalendarInterpretationFailureResponse>(StatusCodes.Status503ServiceUnavailable)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> InterpretAsync(
        CreateCalendarInterpretationRequest? request,
        ICalendarApiService calendarService,
        ICalendarTextInterpreter interpreter,
        TimeZoneInfo timeZone,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var validRequest = request!;
        var selectedCalendar = calendarService.ListSummaries().FirstOrDefault(calendar =>
            string.Equals(calendar.Name, validRequest.DestinationCalendar.Trim(), StringComparison.OrdinalIgnoreCase));
        if (selectedCalendar is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(validRequest.DestinationCalendar)] = ["The destination calendar ID was not found."]
            });
        }

        var input = new CalendarTextInterpretationInput(
            validRequest.Text.Trim(),
            selectedCalendar.Name,
            timeZone,
            timeProvider.GetUtcNow());
        var result = await interpreter.InterpretAsync(input, cancellationToken);
        if (result.Failure is not null)
        {
            var failure = new CalendarInterpretationFailureResponse(result.Failure.Code, result.Failure.Message);
            return result.Failure.Code == "invalid_model_output"
                ? TypedResults.UnprocessableEntity(failure)
                : TypedResults.Json(failure, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return TypedResults.Ok(CalendarInterpretationResponse.FromResult(selectedCalendar.Name, result));
    }

    private static Dictionary<string, string[]> Validate(CreateCalendarInterpretationRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["An interpretation request is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > MaxTextLength)
        {
            errors[nameof(request.Text)] = [$"Calendar text between 1 and {MaxTextLength} characters is required."];
        }

        if (string.IsNullOrWhiteSpace(request.DestinationCalendar) || request.DestinationCalendar.Trim().Length > 100)
        {
            errors[nameof(request.DestinationCalendar)] = ["A destination calendar ID of at most 100 characters is required."];
        }

        return errors;
    }
}
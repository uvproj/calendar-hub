using System.Net.Mail;
using Calendar.Api.Contracts;
using Calendar.Api.Services;
using Calendar.Core;

namespace Calendar.Api.Endpoints;

internal static class CalendarEndpoints
{
    internal static WebApplication MapCalendarEndpoints(this WebApplication app)
    {
        app.MapGet("/api/calendars", (ICalendarApiService calendarService) =>
                TypedResults.Ok(calendarService.ListSummaries()
                    .Select(CalendarSummaryResponse.FromCore)
                    .ToArray()))
            .RequireAuthorization()
            .WithSummary("Lists configured calendars.")
            .WithDescription("Returns safe calendar summaries without provider paths or credentials.")
            .Produces<IReadOnlyList<CalendarSummaryResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/events", GetEventsAsync)
            .RequireAuthorization()
            .WithSummary("Gets events across calendars.")
            .WithDescription("Queries a half-open ISO 8601 range of at most 366 days and preserves per-calendar failures.")
            .Produces<AggregateEventsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/events/batch", CreateBatchAsync)
            .RequireAuthorization()
            .RequireAntiforgery()
            .WithSummary("Creates a confirmed event batch.")
            .WithDescription("Executes structured event drafts against one explicit destination calendar and returns ordered item outcomes.")
            .Produces<BatchCreationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> GetEventsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        string[]? calendar,
        ICalendarApiService calendarService,
        CancellationToken cancellationToken)
    {
        if (start >= end)
        {
            return InvalidRequest("The range start must be before the range end.");
        }

        if (end - start > TimeSpan.FromDays(366))
        {
            return InvalidRequest("The requested range cannot exceed 366 days.");
        }

        try
        {
            var result = await calendarService.QueryAsync(start, end, calendar, cancellationToken);
            return TypedResults.Ok(AggregateEventsResponse.FromCore(result));
        }
        catch (ArgumentException)
        {
            return InvalidRequest("The calendar selection or date range is invalid.");
        }
    }

    private static async Task<IResult> CreateBatchAsync(
        ConfirmBatchCreationRequest request,
        ICalendarApiService calendarService,
        CancellationToken cancellationToken)
    {
        var errors = ValidateBatch(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var drafts = request.Events.Select(draft => new CalendarEventCreateRequest(
                draft.Name.Trim(),
                TrimToNull(draft.Description),
                TrimToNull(draft.Location),
                draft.StartsAt,
                draft.EndsAt,
                draft.IsAllDay,
                draft.Invitees.Select(invitee => invitee.Trim()).ToList())).ToArray();
            var result = await calendarService.CreateBatchAsync(
                request.DestinationCalendar.Trim(),
                drafts,
                cancellationToken);
            return TypedResults.Ok(BatchCreationResponse.FromCore(result));
        }
        catch (ArgumentException)
        {
            return InvalidRequest("The destination calendar or event batch is invalid.");
        }
    }

    private static Dictionary<string, string[]> ValidateBatch(ConfirmBatchCreationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.DestinationCalendar) || request.DestinationCalendar.Trim().Length > 100)
        {
            errors[nameof(request.DestinationCalendar)] = ["A destination calendar of at most 100 characters is required."];
        }

        if (request.Events is null || request.Events.Count is 0 or > CalendarBatchCreationService.MaxBatchSize)
        {
            errors[nameof(request.Events)] = [$"Between 1 and {CalendarBatchCreationService.MaxBatchSize} events are required."];
            return errors;
        }

        for (var index = 0; index < request.Events.Count; index++)
        {
            var draft = request.Events[index];
            var key = $"events[{index}]";
            if (draft is null)
            {
                errors[key] = ["An event draft is required."];
                continue;
            }

            if (string.IsNullOrWhiteSpace(draft.Name) || draft.Name.Trim().Length > 200)
            {
                errors[$"{key}.name"] = ["An event name of at most 200 characters is required."];
            }

            if (draft.Description?.Length > 4000)
            {
                errors[$"{key}.description"] = ["Description cannot exceed 4000 characters."];
            }

            if (draft.Location?.Length > 500)
            {
                errors[$"{key}.location"] = ["Location cannot exceed 500 characters."];
            }

            if (draft.Invitees is null || draft.Invitees.Count > 100 || draft.Invitees.Any(invitee =>
                    string.IsNullOrWhiteSpace(invitee) ||
                    invitee.Length > 254 ||
                    !MailAddress.TryCreate(invitee.Trim(), out _)))
            {
                errors[$"{key}.invitees"] = ["Invitees must contain at most 100 valid email addresses."];
            }

            if (draft.EndsAt <= draft.StartsAt ||
                (draft.IsAllDay && (draft.StartsAt.TimeOfDay != TimeSpan.Zero || draft.EndsAt.TimeOfDay != TimeSpan.Zero)))
            {
                errors[$"{key}.endsAt"] = ["The event interval or all-day boundaries are invalid."];
            }
        }

        return errors;
    }

    private static IResult InvalidRequest(string title) => TypedResults.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: title);

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
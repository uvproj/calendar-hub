using System.Text;
using Calendar.Core;
using Microsoft.Extensions.AI;

namespace Calendar.Api.Ai;

/// <summary>
/// Interprets calendar text through an OpenAI-compatible <see cref="IChatClient"/>.
/// </summary>
public sealed class OpenAiCalendarTextInterpreter : ICalendarTextInterpreter
{
    private readonly IChatClient _chatClient;
    private readonly CalendarTextInterpretationParser _parser;
    private readonly ILogger<OpenAiCalendarTextInterpreter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiCalendarTextInterpreter"/> class.
    /// </summary>
    /// <param name="chatClient">The configured chat client.</param>
    /// <param name="parser">The strict structured-output parser.</param>
    /// <param name="logger">The safe diagnostic logger.</param>
    public OpenAiCalendarTextInterpreter(
        IChatClient chatClient,
        CalendarTextInterpretationParser parser,
        ILogger<OpenAiCalendarTextInterpreter> logger)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(logger);
        _chatClient = chatClient;
        _parser = parser;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CalendarTextInterpretationResult> InterpretAsync(
        CalendarTextInterpretationInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var messages = new ChatMessage[]
            {
                new(ChatRole.System, BuildSystemPrompt(input)),
                new(ChatRole.User, input.Text)
            };
            var response = await _chatClient.GetResponseAsync(
                messages,
                new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
                cancellationToken).ConfigureAwait(false);
            return _parser.Parse(response.Text, input.CurrentInstant);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Calendar AI provider request failed with exception type {ExceptionType}.",
                exception.GetType().Name);
            return new CalendarTextInterpretationResult(
                [],
                [],
                [],
                new CalendarTextInterpretationFailure(
                    "provider_unavailable",
                    "Calendar text interpretation is temporarily unavailable."));
        }
    }

    private static string BuildSystemPrompt(CalendarTextInterpretationInput input)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Convert the user's calendar text to strict JSON only.");
        prompt.Append("Current instant: ").AppendLine(input.CurrentInstant.ToString("O"));
        prompt.Append("Configured time zone ID: ").AppendLine(input.TimeZone.Id);
        prompt.Append("Explicit destination calendar: ").AppendLine(input.DestinationCalendar);
        prompt.Append("Return at most ")
            .Append(CalendarBatchCreationService.MaxBatchSize)
            .AppendLine(" drafts in source order.");
        prompt.AppendLine("Use exactly this JSON shape and no unknown fields:");
        prompt.AppendLine("{\"drafts\":[{\"name\":\"string\",\"description\":null,\"location\":null,\"startsAt\":\"ISO-8601 with offset\",\"endsAt\":\"ISO-8601 with offset\",\"isAllDay\":false,\"invitees\":[\"email\"]}],\"warnings\":[],\"clarificationErrors\":[]}");
        prompt.AppendLine("description and location may be strings or null. Timed endsAt must be after startsAt. All-day values must use midnight boundaries and an exclusive end date.");
        prompt.AppendLine("Resolve dates only within two years before or after the current instant. If required details are missing, return no guessed draft and add a concise clarificationErrors item.");
        prompt.AppendLine("Do not return operation, delete, update, command, or calendar-choice fields. Never change the explicit destination calendar.");
        return prompt.ToString();
    }
}
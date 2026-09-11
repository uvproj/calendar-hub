using System.Runtime.CompilerServices;
using Calendar.Api.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Calendar.Api.Tests;

public sealed class CalendarTextInterpreterTests
{
    [Fact]
    public async Task FakeInterpreter_DocumentedInput_ReturnsDeterministicPreview()
    {
        var interpreter = new FakeCalendarTextInterpreter();
        var input = new CalendarTextInterpretationInput(
            "sample: dinner-and-picnic",
            "Family",
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));

        var result = await interpreter.InterpretAsync(input, CancellationToken.None);

        Assert.Equal(["Dinner", "Picnic"], result.Drafts.Select(draft => draft.Name));
    }

    [Fact]
    public async Task OpenAiInterpreter_ProviderThrows_ReturnsAndLogsOnlySafeDetails()
    {
        var logger = new CapturingLogger<OpenAiCalendarTextInterpreter>();
        var interpreter = new OpenAiCalendarTextInterpreter(
            new ThrowingChatClient(),
            new CalendarTextInterpretationParser(),
            logger);
        var input = new CalendarTextInterpretationInput(
            "raw-prompt-secret",
            "Family",
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));

        var result = await interpreter.InterpretAsync(input, CancellationToken.None);
        var diagnosticText = string.Join(Environment.NewLine, logger.Messages);

        Assert.Equal("provider_unavailable", result.Failure?.Code);
        Assert.DoesNotContain("raw-prompt-secret", diagnosticText, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-provider-secret", diagnosticText, StringComparison.Ordinal);
        Assert.DoesNotContain("api-key-secret", diagnosticText, StringComparison.Ordinal);
    }

    private sealed class ThrowingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("raw-provider-secret api-key-secret");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        internal List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
namespace CalendarCli.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCollection
{
    public const string Name = "Console";
}

[Collection(ConsoleCollection.Name)]
public sealed class CalendarConsoleTests
{
    [Theory]
    [InlineData("Sprint Planning")]
    [InlineData("Sprint", "Planning")]
    public async Task RunAsync_AddWithPositionalName_AcceptsName(params string[] nameParts)
    {
        var missingService = $"__missing_{Guid.NewGuid():N}";
        var args = new[] { "events", "add" }
            .Concat(nameParts)
            .Concat(["--when", "tomorrow", "--service", missingService])
            .ToArray();

        var (exitCode, error) = await RunAndCaptureErrorAsync(args);

        Assert.Equal(1, exitCode);
        Assert.Contains($"Service '{missingService}' was not found.", error);
    }

    [Fact]
    public async Task RunAsync_AddWithNameOption_AcceptsLegacySyntax()
    {
        var missingService = $"__missing_{Guid.NewGuid():N}";
        string[] args = [
            "events", "add", "--name", "Sprint Planning", "--when", "tomorrow", "--service", missingService
        ];

        var (exitCode, error) = await RunAndCaptureErrorAsync(args);

        Assert.Equal(1, exitCode);
        Assert.Contains($"Service '{missingService}' was not found.", error);
    }

    [Fact]
    public async Task RunAsync_AddWithPositionalAndOptionNames_ReturnsAmbiguityError()
    {
        string[] args = [
            "events", "add", "Positional Name", "--name", "Option Name", "--when", "tomorrow"
        ];

        var (exitCode, error) = await RunAndCaptureErrorAsync(args);

        Assert.Equal(1, exitCode);
        Assert.Contains("Specify the event name either as positional text or with --name, not both.", error);
    }

    [Fact]
    public async Task RunAsync_AddWithoutName_ReturnsRequiredNameError()
    {
        string[] args = ["events", "add", "--when", "tomorrow"];

        var (exitCode, error) = await RunAndCaptureErrorAsync(args);

        Assert.Equal(1, exitCode);
        Assert.Contains("The event name is required.", error);
    }

    [Fact]
    public async Task RunAsync_ListWithUnknownOption_ReturnsUsageError()
    {
        string[] args = ["events", "list", "--unknown", "value"];

        var (exitCode, error) = await RunAndCaptureErrorAsync(args);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown option(s): --unknown", error);
    }

    [Fact]
    public async Task RunAsync_DeleteWithMissingService_ReturnsNotFoundError()
    {
        var missingService = $"__missing_{Guid.NewGuid():N}";
        string[] args = ["events", "delete", "--id", "event-id", "--service", missingService];

        var (exitCode, error) = await RunAndCaptureErrorAsync(args);

        Assert.Equal(1, exitCode);
        Assert.Contains($"Service '{missingService}' was not found.", error);
    }

    private static async Task<(int ExitCode, string Error)> RunAndCaptureErrorAsync(string[] args)
    {
        var originalError = Console.Error;
        var originalOutput = Console.Out;
        using var errorWriter = new StringWriter();
        using var outputWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            Console.SetOut(outputWriter);
            var exitCode = await CalendarConsole.RunAsync(args);
            return (exitCode, errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetOut(originalOutput);
        }
    }
}

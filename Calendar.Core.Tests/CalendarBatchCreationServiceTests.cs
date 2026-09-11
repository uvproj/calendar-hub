namespace Calendar.Core.Tests;

public sealed class CalendarBatchCreationServiceTests : IDisposable
{
    private static readonly DateTimeOffset EventStart = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        $"calendar-batch-tests-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAsync_MissingDestination_ThrowsBeforeProviderCalls(string? destination)
    {
        var provider = SuccessfulProvider();
        var service = CreateService(provider);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.CreateAsync(destination!, [CreateRequest("One")]));

        Assert.Equal(0, provider.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_UnknownDestination_ThrowsBeforeProviderCalls()
    {
        var provider = SuccessfulProvider();
        var service = CreateService(provider);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("Missing", [CreateRequest("One")]));

        Assert.Equal(0, provider.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_NullRequests_ThrowsBeforeProviderCalls()
    {
        var provider = SuccessfulProvider();
        var service = CreateService(provider);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CreateAsync("Family", null!));

        Assert.Equal(0, provider.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_EmptyRequests_ThrowsBeforeProviderCalls()
    {
        var provider = SuccessfulProvider();
        var service = CreateService(provider);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("Family", Array.Empty<CalendarEventCreateRequest>()));

        Assert.Equal(0, provider.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_OversizedRequests_ThrowsBeforeProviderCalls()
    {
        var provider = SuccessfulProvider();
        var service = CreateService(provider);
        var requests = Enumerable.Range(0, CalendarBatchCreationService.MaxBatchSize + 1)
            .Select(index => CreateRequest($"Event {index}"))
            .ToArray();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("Family", requests));

        Assert.Equal(0, provider.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_NullItem_ThrowsBeforeProviderCalls()
    {
        var provider = SuccessfulProvider();
        var service = CreateService(provider);
        CalendarEventCreateRequest[] requests = [CreateRequest("One"), null!];

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("Family", requests));

        Assert.Equal(0, provider.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_PartialFailure_ReturnsOrderedResultsAndContinues()
    {
        var callIndex = 0;
        var provider = new FakeCalendarService
        {
            AddHandler = (request, _) =>
            {
                var currentIndex = callIndex++;
                return currentIndex == 1
                    ? throw new InvalidOperationException("credential=secret-value")
                    : Task.FromResult(CreateEvent($"id-{currentIndex}", request));
            }
        };
        var service = CreateService(provider);

        var result = await service.CreateAsync(
            " family ",
            [CreateRequest("First"), CreateRequest("Second"), CreateRequest("Third")]);

        Assert.Equal("Family", result.DestinationCalendarName);
        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal(0, item.Index);
                Assert.Equal("id-0", item.Event!.Id);
                Assert.Null(item.Error);
            },
            item =>
            {
                Assert.Equal(1, item.Index);
                Assert.Null(item.Event);
                Assert.Equal(CalendarOperationErrorCategory.Provider, item.Error!.Category);
                Assert.DoesNotContain("secret-value", item.Error.Message, StringComparison.Ordinal);
            },
            item =>
            {
                Assert.Equal(2, item.Index);
                Assert.Equal("id-2", item.Event!.Id);
                Assert.Null(item.Error);
            });
        Assert.Equal(3, provider.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_CallerCancellation_PropagatesAndStopsLaterItems()
    {
        using var cancellationSource = new CancellationTokenSource();
        var provider = new FakeCalendarService
        {
            AddHandler = (request, cancellationToken) =>
            {
                cancellationSource.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(CreateEvent("unused", request));
            }
        };
        var service = CreateService(provider);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync(
            "Family",
            [CreateRequest("First"), CreateRequest("Second")],
            cancellationSource.Token));

        Assert.Equal(1, provider.AddCallCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }

    private CalendarBatchCreationService CreateService(FakeCalendarService provider)
    {
        var store = new ServiceStore(Path.Combine(_directoryPath, "services.json"));
        store.Add(new Service
        {
            Name = "Family",
            Type = ServiceType.FileSystem,
            FilePath = "family.json"
        });
        var factory = new CalendarServiceFactory(store, _ => provider);
        return new CalendarBatchCreationService(factory);
    }

    private static FakeCalendarService SuccessfulProvider() =>
        new()
        {
            AddHandler = (request, _) => Task.FromResult(CreateEvent("created", request))
        };

    private static CalendarEventCreateRequest CreateRequest(string name) =>
        new(name, null, null, EventStart, EventStart.AddHours(1), false, []);

    private static CalendarEvent CreateEvent(string id, CalendarEventCreateRequest request) =>
        new(
            id,
            request.Name,
            request.Description,
            request.Location,
            request.StartsAt,
            request.EndsAt,
            request.IsAllDay,
            request.Invitees.ToList());
}
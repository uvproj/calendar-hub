namespace Calendar.Core.Tests;

public sealed class CalendarAggregationServiceTests : IDisposable
{
    private static readonly DateTimeOffset RangeStart = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeEnd = RangeStart.AddDays(7);
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        $"calendar-aggregation-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task QueryAsync_AllCalendars_ReturnsSourceAwareEvents()
    {
        var familyEvent = CreateEvent("shared-id", "Breakfast", RangeStart.AddHours(8));
        var workEvent = CreateEvent("shared-id", "Planning", RangeStart.AddHours(9));
        var service = CreateService(
            ("Family", ServiceType.FileSystem, SuccessfulProvider(familyEvent)),
            ("Work", ServiceType.Google, SuccessfulProvider(workEvent)));

        var result = await service.QueryAsync(RangeStart, RangeEnd);

        Assert.Collection(
            result.Events,
            item =>
            {
                Assert.Equal("Family", item.SourceCalendarName);
                Assert.Equal(ServiceType.FileSystem, item.SourceCalendarType);
                Assert.Equal(new CalendarEventIdentity("Family", "shared-id"), item.Identity);
                Assert.Same(familyEvent, item.Event);
            },
            item =>
            {
                Assert.Equal("Work", item.SourceCalendarName);
                Assert.Equal(ServiceType.Google, item.SourceCalendarType);
                Assert.Equal(new CalendarEventIdentity("Work", "shared-id"), item.Identity);
                Assert.Same(workEvent, item.Event);
            });
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task QueryAsync_AllCalendars_StartsProviderQueriesConcurrently()
    {
        var bothProvidersStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedCount = 0;
        FakeCalendarService CreateWaitingProvider() => new()
        {
            ListHandler = async (_, _, cancellationToken) =>
            {
                if (Interlocked.Increment(ref startedCount) == 2)
                {
                    bothProvidersStarted.SetResult();
                }

                await bothProvidersStarted.Task.WaitAsync(cancellationToken);
                return Array.Empty<CalendarEvent>();
            }
        };
        var service = CreateService(
            ("Family", ServiceType.FileSystem, CreateWaitingProvider()),
            ("Work", ServiceType.Google, CreateWaitingProvider()));
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.QueryAsync(RangeStart, RangeEnd, cancellationToken: cancellationSource.Token);

        Assert.Equal(2, startedCount);
    }

    [Fact]
    public async Task QueryAsync_SelectedCalendars_ReturnsCanonicalNames()
    {
        var familyProvider = SuccessfulProvider(CreateEvent("1", "Family event", RangeStart.AddHours(8)));
        var workProvider = SuccessfulProvider(CreateEvent("2", "Work event", RangeStart.AddHours(9)));
        var service = CreateService(
            ("Family", ServiceType.FileSystem, familyProvider),
            ("Work", ServiceType.Google, workProvider));

        var result = await service.QueryAsync(RangeStart, RangeEnd, [" work "]);

        var calendarEvent = Assert.Single(result.Events);
        Assert.Equal("Work", calendarEvent.SourceCalendarName);
        Assert.Equal(0, familyProvider.ListCallCount);
        Assert.Equal(1, workProvider.ListCallCount);
    }

    [Fact]
    public async Task QueryAsync_UnorderedProviderResults_ReturnsDeterministicOrder()
    {
        var service = CreateService(
            ("Zulu", ServiceType.Google, SuccessfulProvider(
                CreateEvent("2", "Same", RangeStart.AddHours(10)))),
            ("Alpha", ServiceType.FileSystem, SuccessfulProvider(
                CreateEvent("2", "Zulu name", RangeStart.AddHours(10)),
                CreateEvent("1", "Alpha name", RangeStart.AddHours(10)),
                CreateEvent("3", "Earlier", RangeStart.AddHours(8)))));

        var result = await service.QueryAsync(RangeStart, RangeEnd);

        Assert.Equal(
            ["Alpha:3", "Alpha:1", "Alpha:2", "Zulu:2"],
            result.Events.Select(item => $"{item.SourceCalendarName}:{item.Event.Id}"));
    }

    [Fact]
    public async Task QueryAsync_ProviderFailure_ReturnsOtherEventsAndSafeError()
    {
        var failedProvider = new FakeCalendarService
        {
            ListHandler = (_, _, _) => throw new InvalidOperationException("token=secret-value")
        };
        var service = CreateService(
            ("Broken", ServiceType.Google, failedProvider),
            ("Family", ServiceType.FileSystem, SuccessfulProvider(
                CreateEvent("1", "Available", RangeStart.AddHours(8)))));

        var result = await service.QueryAsync(RangeStart, RangeEnd);

        Assert.Single(result.Events);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Broken", error.CalendarName);
        Assert.Equal(ServiceType.Google, error.CalendarType);
        Assert.Equal(CalendarOperationErrorCategory.Provider, error.Error.Category);
        Assert.DoesNotContain("secret-value", error.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_UnknownSelection_ThrowsBeforeProviderCalls()
    {
        var provider = SuccessfulProvider();
        var service = CreateService(("Family", ServiceType.FileSystem, provider));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryAsync(RangeStart, RangeEnd, ["Missing"]));

        Assert.Equal(0, provider.ListCallCount);
    }

    [Fact]
    public async Task QueryAsync_DuplicateSelection_ThrowsBeforeProviderCalls()
    {
        var provider = SuccessfulProvider();
        var service = CreateService(("Family", ServiceType.FileSystem, provider));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryAsync(RangeStart, RangeEnd, ["Family", " family "]));

        Assert.Equal(0, provider.ListCallCount);
    }

    [Fact]
    public async Task QueryAsync_InvalidRange_ThrowsBeforeProviderCalls()
    {
        var provider = SuccessfulProvider();
        var service = CreateService(("Family", ServiceType.FileSystem, provider));

        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(RangeEnd, RangeStart));

        Assert.Equal(0, provider.ListCallCount);
    }

    [Fact]
    public async Task QueryAsync_CallerCancellation_PropagatesCancellation()
    {
        var provider = new FakeCalendarService
        {
            ListHandler = async (_, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Array.Empty<CalendarEvent>();
            }
        };
        var service = CreateService(("Family", ServiceType.FileSystem, provider));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.QueryAsync(RangeStart, RangeEnd, cancellationToken: cancellationSource.Token));
    }

    [Fact]
    public async Task QueryAsync_NoConfiguredCalendars_UsesLocalFallback()
    {
        Service? resolvedService = null;
        var provider = SuccessfulProvider(CreateEvent("1", "Local", RangeStart.AddHours(8)));
        var factory = new CalendarServiceFactory(CreateStore(), serviceConfiguration =>
        {
            resolvedService = serviceConfiguration;
            return provider;
        });
        var service = new CalendarAggregationService(factory);

        var result = await service.QueryAsync(RangeStart, RangeEnd);

        var calendarEvent = Assert.Single(result.Events);
        Assert.Equal("Local", calendarEvent.SourceCalendarName);
        Assert.Equal(ServiceType.FileSystem, calendarEvent.SourceCalendarType);
        Assert.Equal("Local", resolvedService!.Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }

    private CalendarAggregationService CreateService(
        params (string Name, ServiceType Type, FakeCalendarService Provider)[] calendars)
    {
        var store = CreateStore();
        foreach (var calendar in calendars)
        {
            store.Add(new Service
            {
                Name = calendar.Name,
                Type = calendar.Type,
                FilePath = calendar.Type == ServiceType.FileSystem ? $"{calendar.Name}.json" : null,
                SecretsJsonPath = calendar.Type == ServiceType.Google ? $"{calendar.Name}.json" : null
            });
        }

        var providers = calendars.ToDictionary(
            calendar => calendar.Name,
            calendar => (ICalendarService)calendar.Provider,
            StringComparer.OrdinalIgnoreCase);
        var factory = new CalendarServiceFactory(store, service => providers[service.Name]);
        return new CalendarAggregationService(factory);
    }

    private ServiceStore CreateStore() =>
        new(Path.Combine(_directoryPath, $"services-{Guid.NewGuid():N}.json"));

    private static FakeCalendarService SuccessfulProvider(params CalendarEvent[] events) =>
        new()
        {
            ListHandler = (_, _, _) => Task.FromResult<IReadOnlyList<CalendarEvent>>(events)
        };

    private static CalendarEvent CreateEvent(string id, string name, DateTimeOffset startsAt) =>
        new(id, name, null, null, startsAt, startsAt.AddHours(1), false, []);
}
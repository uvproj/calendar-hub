namespace Calendar.Core.Tests;

public sealed class CalendarServiceFactoryTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        $"calendar-factory-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Resolve_ExplicitName_UsesCaseInsensitiveConfiguredService()
    {
        var store = CreateStore();
        store.Add(new Service
        {
            Name = "Family",
            Type = ServiceType.Google,
            SecretsJsonPath = "secrets.json",
            CalendarId = "family@example.com"
        });
        Service? resolvedConfiguration = null;
        var expected = new StubCalendarService();
        var factory = new CalendarServiceFactory(store, service =>
        {
            resolvedConfiguration = service;
            return expected;
        });

        var actual = factory.Resolve(" family ");

        Assert.Same(expected, actual);
        Assert.Equal("Family", resolvedConfiguration!.Name);
    }

    [Fact]
    public void Resolve_OmittedName_UsesConfiguredDefault()
    {
        var store = CreateStore();
        store.Add(new Service
        {
            Name = "First",
            Type = ServiceType.FileSystem,
            FilePath = "first.json"
        });
        store.Add(new Service
        {
            Name = "Preferred",
            Type = ServiceType.FileSystem,
            FilePath = "preferred.json",
            IsDefault = true
        });
        Service? resolvedConfiguration = null;
        var factory = new CalendarServiceFactory(store, service =>
        {
            resolvedConfiguration = service;
            return new StubCalendarService();
        });

        factory.Resolve();

        Assert.Equal("Preferred", resolvedConfiguration!.Name);
    }

    [Fact]
    public void Resolve_NoConfiguredService_UsesFallbackEventsFile()
    {
        Service? resolvedConfiguration = null;
        var factory = new CalendarServiceFactory(CreateStore(), service =>
        {
            resolvedConfiguration = service;
            return new StubCalendarService();
        });

        factory.Resolve();

        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "calendar-cli",
            "events.json");
        Assert.Equal(expectedPath, resolvedConfiguration!.FilePath);
    }

    [Fact]
    public void Resolve_MissingExplicitService_ThrowsPreservedMessage()
    {
        var factory = new CalendarServiceFactory(CreateStore(), _ => new StubCalendarService());

        var exception = Assert.Throws<CalendarServiceConfigurationException>(() => factory.Resolve("Missing"));

        Assert.Equal("Service 'Missing' was not found.", exception.Message);
    }

    [Fact]
    public void Resolve_FileSystemWithoutPath_ThrowsPreservedMessage()
    {
        var store = CreateStoreWithJson("""
            [{ "Name": "Broken", "Type": 0, "IsDefault": true }]
            """);
        var factory = new CalendarServiceFactory(store);

        var exception = Assert.Throws<CalendarServiceConfigurationException>(() => factory.Resolve());

        Assert.Equal("File path is missing for FileSystem service.", exception.Message);
    }

    [Fact]
    public void Resolve_UnsupportedType_ThrowsPreservedMessage()
    {
        var store = CreateStoreWithJson("""
            [{ "Name": "Future", "Type": 99, "IsDefault": true }]
            """);
        var factory = new CalendarServiceFactory(store);

        var exception = Assert.Throws<CalendarServiceConfigurationException>(() => factory.Resolve());

        Assert.Equal("Unsupported service type '99'.", exception.Message);
    }

    [Fact]
    public void ListSummaries_ConfiguredServices_ExposesOnlySafeFields()
    {
        var store = CreateStore();
        store.Add(new Service
        {
            Name = "Family",
            Type = ServiceType.Google,
            SecretsJsonPath = "private-secrets.json"
        });
        var factory = new CalendarServiceFactory(store, _ => new StubCalendarService());

        var summary = Assert.Single(factory.ListSummaries());

        Assert.Equal(new CalendarServiceSummary("Family", ServiceType.Google, true), summary);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }

    private ServiceStore CreateStore()
    {
        return new ServiceStore(Path.Combine(_directoryPath, "services.json"));
    }

    private ServiceStore CreateStoreWithJson(string json)
    {
        var filePath = Path.Combine(_directoryPath, $"services-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(_directoryPath);
        File.WriteAllText(filePath, json);
        return new ServiceStore(filePath);
    }

    private sealed class StubCalendarService : ICalendarService
    {
        public Task<CalendarEvent> AddAsync(
            CalendarEventCreateRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CalendarEvent>> ListBetweenAsync(
            DateTimeOffset startInclusive,
            DateTimeOffset endExclusive,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(bool Succeeded, CalendarEvent? DeletedEvent)> DeleteAsync(
            string eventId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
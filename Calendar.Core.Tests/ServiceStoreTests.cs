namespace Calendar.Core.Tests;

public sealed class ServiceStoreTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        $"calendar-core-tests-{Guid.NewGuid():N}");

    [Fact]
    public void GetAndList_ConfiguredServices_ReturnsTrimmedDetachedServicesInPersistedOrder()
    {
        var store = CreateStore();
        store.Add(CreateFileSystemService(" First ", " first.json "));
        store.Add(CreateGoogleService("Second", " secrets.json "));

        var found = store.Get(" first ");
        var listed = store.List();

        Assert.Equal("First", found!.Name);
        Assert.Equal(["First", "Second"], listed.Select(service => service.Name));
        Assert.Equal("first.json", listed[0].FilePath);
        Assert.Equal("secrets.json", listed[1].SecretsJsonPath);

        found.Name = "Changed";
        Assert.Equal("First", store.Get("FIRST")!.Name);
    }

    [Fact]
    public void Get_MissingService_ReturnsNull()
    {
        var store = CreateStore();

        var service = store.Get("missing");

        Assert.Null(service);
    }

    [Fact]
    public void Add_FirstService_MakesServiceDefault()
    {
        var store = CreateStore();

        var added = store.Add(CreateFileSystemService("Personal", "events.json"));

        Assert.True(added.IsDefault);
    }

    [Fact]
    public void Add_ServiceRequestingDefault_ReplacesExistingDefault()
    {
        var store = CreateStore();
        store.Add(CreateFileSystemService("Personal", "personal.json"));

        store.Add(CreateGoogleService("Work", "secrets.json", isDefault: true));

        Assert.False(store.Get("Personal")!.IsDefault);
        Assert.True(store.Get("Work")!.IsDefault);
    }

    [Fact]
    public void Add_CaseInsensitiveDuplicate_ThrowsArgumentException()
    {
        var store = CreateStore();
        store.Add(CreateFileSystemService("Personal", "personal.json"));

        var exception = Assert.Throws<ArgumentException>(() =>
            store.Add(CreateFileSystemService(" personal ", "other.json")));

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public void Add_BlankName_ThrowsArgumentException()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentException>(() =>
            store.Add(CreateFileSystemService(" ", "events.json")));
    }

    [Theory]
    [InlineData(ServiceType.FileSystem)]
    [InlineData(ServiceType.Google)]
    public void Add_MissingProviderPath_ThrowsArgumentException(ServiceType serviceType)
    {
        var store = CreateStore();
        var service = new Service
        {
            Name = "Invalid",
            Type = serviceType
        };

        Assert.Throws<ArgumentException>(() => store.Add(service));
    }

    [Fact]
    public void Delete_DefaultService_RemovesServiceAndPromotesFirstRemainingService()
    {
        var store = CreateStore();
        store.Add(CreateFileSystemService("First", "first.json"));
        store.Add(CreateFileSystemService("Second", "second.json"));

        var deleted = store.Delete(" first ");

        Assert.Equal("First", deleted!.Name);
        Assert.True(store.Get("Second")!.IsDefault);
    }

    [Fact]
    public void Delete_MissingService_ReturnsNull()
    {
        var store = CreateStore();

        var deleted = store.Delete("missing");

        Assert.Null(deleted);
    }

    [Fact]
    public void SetDefault_ExistingService_SelectsOnlyRequestedService()
    {
        var store = CreateStore();
        store.Add(CreateFileSystemService("First", "first.json"));
        store.Add(CreateFileSystemService("Second", "second.json"));

        var selected = store.SetDefault(" second ");

        Assert.Equal("Second", selected!.Name);
        Assert.False(store.Get("First")!.IsDefault);
        Assert.True(store.Get("Second")!.IsDefault);
    }

    [Fact]
    public void SetDefault_MissingService_ReturnsNull()
    {
        var store = CreateStore();

        var selected = store.SetDefault("missing");

        Assert.Null(selected);
    }

    [Fact]
    public void Add_NewStoreInstance_PersistsServices()
    {
        var filePath = CreateFilePath();
        var store = new ServiceStore(filePath);
        store.Add(CreateFileSystemService("Personal", "events.json"));

        var reloadedStore = new ServiceStore(filePath);

        Assert.Equal("Personal", Assert.Single(reloadedStore.List()).Name);
    }

    [Fact]
    public void Add_GoogleCalendarId_PersistsTrimmedValue()
    {
        var filePath = CreateFilePath();
        var store = new ServiceStore(filePath);
        var service = CreateGoogleService("Family", "secrets.json");
        service.CalendarId = " family@example.com ";

        store.Add(service);

        Assert.Equal("family@example.com", new ServiceStore(filePath).Get("Family")!.CalendarId);
    }

    [Fact]
    public void Add_GoogleWithoutCalendarId_PreservesPrimaryDefaultBehavior()
    {
        var store = CreateStore();

        var added = store.Add(CreateGoogleService("Family", "secrets.json"));

        Assert.Equal("primary", added.CalendarId);
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
        return new ServiceStore(CreateFilePath());
    }

    private string CreateFilePath()
    {
        return Path.Combine(_directoryPath, "services.json");
    }

    private static Service CreateFileSystemService(string name, string filePath, bool isDefault = false)
    {
        return new Service
        {
            Name = name,
            Type = ServiceType.FileSystem,
            FilePath = filePath,
            IsDefault = isDefault
        };
    }

    private static Service CreateGoogleService(string name, string secretsJsonPath, bool isDefault = false)
    {
        return new Service
        {
            Name = name,
            Type = ServiceType.Google,
            SecretsJsonPath = secretsJsonPath,
            IsDefault = isDefault
        };
    }
}
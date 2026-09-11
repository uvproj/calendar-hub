namespace Calendar.Core;

/// <summary>
/// Resolves configured calendar services and exposes safe service summaries.
/// </summary>
public sealed class CalendarServiceFactory
{
    private readonly ServiceStore _serviceStore;
    private readonly Func<Service, ICalendarService> _createProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarServiceFactory"/> class using the default service store.
    /// </summary>
    public CalendarServiceFactory()
        : this(new ServiceStore())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarServiceFactory"/> class.
    /// </summary>
    /// <param name="serviceStore">The configured calendar service store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceStore"/> is <see langword="null"/>.</exception>
    public CalendarServiceFactory(ServiceStore serviceStore)
        : this(serviceStore, CreateProvider)
    {
    }

    internal CalendarServiceFactory(
        ServiceStore serviceStore,
        Func<Service, ICalendarService> createProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceStore);
        ArgumentNullException.ThrowIfNull(createProvider);

        _serviceStore = serviceStore;
        _createProvider = createProvider;
    }

    /// <summary>
    /// Resolves an explicitly named service or the configured default service.
    /// </summary>
    /// <param name="serviceName">The optional configured service name.</param>
    /// <returns>The resolved calendar provider.</returns>
    /// <exception cref="CalendarServiceConfigurationException">The named service is missing or its provider configuration is invalid.</exception>
    public ICalendarService Resolve(string? serviceName = null)
    {
        Service service;

        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            service = _serviceStore.Get(serviceName) ??
                throw new CalendarServiceConfigurationException($"Service '{serviceName}' was not found.");
        }
        else
        {
            service = _serviceStore.List().FirstOrDefault(candidate => candidate.IsDefault) ?? CreateFallbackService();
        }

        return _createProvider(service);
    }

    /// <summary>
    /// Lists configured services without exposing filesystem or credential paths.
    /// </summary>
    /// <returns>A read-only collection of safe service summaries.</returns>
    public IReadOnlyList<CalendarServiceSummary> ListSummaries()
    {
        return _serviceStore.List()
            .Select(service => new CalendarServiceSummary(service.Name, service.Type, service.IsDefault))
            .ToList()
            .AsReadOnly();
    }

    private static Service CreateFallbackService()
    {
        return new Service
        {
            Name = "Local",
            Type = ServiceType.FileSystem,
            IsDefault = true,
            FilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "calendar-cli",
                "events.json")
        };
    }

    private static ICalendarService CreateProvider(Service service)
    {
        return service.Type switch
        {
            ServiceType.FileSystem => new FileSystemCalendarService(service.FilePath ??
                throw new CalendarServiceConfigurationException("File path is missing for FileSystem service.")),
            ServiceType.Google => new GoogleCalendarService(
                service.Name,
                service.SecretsJsonPath ??
                    throw new CalendarServiceConfigurationException("Secrets JSON path is missing for Google service."),
                service.CalendarId),
            _ => throw new CalendarServiceConfigurationException($"Unsupported service type '{service.Type}'.")
        };
    }
}
using System.Text.Json;

namespace Calendar.Core;

/// <summary>
/// Persists configured calendar services in local application data.
/// </summary>
public sealed class ServiceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceStore"/> class.
    /// </summary>
    public ServiceStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "calendar-cli",
            "services.json"))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceStore"/> class for the specified storage path.
    /// </summary>
    /// <param name="filePath">The JSON file used to persist configured services.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is blank.</exception>
    public ServiceStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("The services file path cannot be blank.", nameof(filePath));
        }

        _filePath = filePath;
    }

    /// <summary>Gets a configured calendar service by name.</summary>
    /// <param name="name">The service name to find.</param>
    /// <returns>The matching service, or <see langword="null"/> when no service is found.</returns>
    public Service? Get(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalizedName = name.Trim();
        var service = LoadAll().FirstOrDefault(candidate =>
            candidate.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

        return service is null ? null : Clone(service);
    }

    /// <summary>Adds and persists a calendar service.</summary>
    /// <param name="service">The service configuration to add.</param>
    /// <returns>The persisted service configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The service name or provider configuration is invalid, or the name already exists.</exception>
    public Service Add(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);

        var normalizedService = NormalizeAndValidate(service);
        var services = LoadAll();
        if (services.Any(existing => existing.Name.Equals(normalizedService.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Service '{normalizedService.Name}' already exists.");
        }

        if (services.Count == 0)
        {
            normalizedService.IsDefault = true;
        }

        if (normalizedService.IsDefault)
        {
            foreach (var existing in services)
            {
                existing.IsDefault = false;
            }
        }

        services.Add(normalizedService);
        SaveAll(services);
        return Clone(normalizedService);
    }

    /// <summary>Lists all configured calendar services in persisted order.</summary>
    /// <returns>A read-only collection of configured services.</returns>
    public IReadOnlyList<Service> List()
    {
        return LoadAll().Select(Clone).ToList().AsReadOnly();
    }

    /// <summary>Deletes a configured calendar service by name.</summary>
    /// <param name="name">The service name to delete.</param>
    /// <returns>The deleted service, or <see langword="null"/> when no service is found.</returns>
    public Service? Delete(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalizedName = name.Trim();
        var services = LoadAll();
        var service = services.FirstOrDefault(candidate =>
            candidate.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
        if (service is null)
        {
            return null;
        }

        services.Remove(service);
        if (service.IsDefault && services.Count > 0)
        {
            services[0].IsDefault = true;
        }

        SaveAll(services);
        return Clone(service);
    }

    /// <summary>Sets the configured calendar service with the specified name as the sole default.</summary>
    /// <param name="name">The service name to select.</param>
    /// <returns>The selected service, or <see langword="null"/> when no service is found.</returns>
    public Service? SetDefault(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalizedName = name.Trim();
        var services = LoadAll();
        var service = services.FirstOrDefault(candidate =>
            candidate.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
        if (service is null)
        {
            return null;
        }

        foreach (var existing in services)
        {
            existing.IsDefault = ReferenceEquals(existing, service);
        }

        SaveAll(services);
        return Clone(service);
    }

    private List<Service> LoadAll()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<Service>>(json, SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void SaveAll(List<Service> services)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(services, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error saving services: {ex.Message}");
        }
    }

    private static Service NormalizeAndValidate(Service service)
    {
        if (string.IsNullOrWhiteSpace(service.Name))
        {
            throw new ArgumentException("The service name cannot be blank.", nameof(service));
        }

        var normalizedService = Clone(service);
        normalizedService.Name = service.Name.Trim();
        normalizedService.FilePath = TrimToNull(service.FilePath);
        normalizedService.SecretsJsonPath = TrimToNull(service.SecretsJsonPath);
        normalizedService.CalendarId = TrimToNull(service.CalendarId) ?? "primary";

        switch (normalizedService.Type)
        {
            case ServiceType.FileSystem when normalizedService.FilePath is null:
                throw new ArgumentException("A file path is required for a FileSystem service.", nameof(service));
            case ServiceType.Google when normalizedService.SecretsJsonPath is null:
                throw new ArgumentException("A secrets JSON path is required for a Google service.", nameof(service));
            case ServiceType.FileSystem:
            case ServiceType.Google:
                return normalizedService;
            default:
                throw new ArgumentOutOfRangeException(nameof(service), service.Type, "The service type is not supported.");
        }
    }

    private static Service Clone(Service service)
    {
        return new Service
        {
            Name = service.Name,
            Type = service.Type,
            IsDefault = service.IsDefault,
            FilePath = service.FilePath,
            SecretsJsonPath = service.SecretsJsonPath,
            CalendarId = TrimToNull(service.CalendarId) ?? "primary"
        };
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
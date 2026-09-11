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
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "calendar-cli");

        _filePath = Path.Combine(root, "services.json");
    }

    /// <summary>Loads all configured calendar services.</summary>
    /// <returns>The configured services, or an empty list when none can be loaded.</returns>
    public List<Service> LoadAll()
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

    /// <summary>Saves all configured calendar services.</summary>
    /// <param name="services">The services to persist.</param>
    public void SaveAll(List<Service> services)
    {
        ArgumentNullException.ThrowIfNull(services);

        try
        {
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(services, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error saving services: {ex.Message}");
        }
    }
}
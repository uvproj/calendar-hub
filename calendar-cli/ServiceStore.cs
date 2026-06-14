using System.Text.Json;

namespace CalendarCli;

internal sealed class ServiceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public ServiceStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "calendar-cli");

        _filePath = Path.Combine(root, "services.json");
    }

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
        catch
        {
            return [];
        }
    }

    public void SaveAll(List<Service> services)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(services, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error saving services: {ex.Message}");
        }
    }
}

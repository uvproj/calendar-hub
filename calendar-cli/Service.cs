namespace CalendarCli;

internal enum ServiceType
{
    FileSystem,
    Google
}

internal sealed class Service
{
    public string Name { get; set; } = string.Empty;
    public ServiceType Type { get; set; }
    public bool IsDefault { get; set; }
    public string? FilePath { get; set; }
    public string? SecretsJsonPath { get; set; }
}

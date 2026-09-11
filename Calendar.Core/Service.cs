namespace Calendar.Core;

/// <summary>
/// Identifies a supported calendar provider type.
/// </summary>
public enum ServiceType
{
    /// <summary>Uses local JSON file persistence.</summary>
    FileSystem,

    /// <summary>Uses Google Calendar.</summary>
    Google
}

/// <summary>
/// Describes a configured calendar provider.
/// </summary>
public sealed class Service
{
    /// <summary>Gets or sets the service name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider type.</summary>
    public ServiceType Type { get; set; }

    /// <summary>Gets or sets a value that indicates whether this is the default service.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Gets or sets the filesystem provider path.</summary>
    public string? FilePath { get; set; }

    /// <summary>Gets or sets the Google OAuth client secrets path.</summary>
    public string? SecretsJsonPath { get; set; }

    /// <summary>Gets or sets the Google calendar identifier.</summary>
    public string CalendarId { get; set; } = "primary";
}
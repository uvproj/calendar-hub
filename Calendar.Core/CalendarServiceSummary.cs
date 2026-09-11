namespace Calendar.Core;

/// <summary>
/// Describes a configured calendar service without exposing provider credentials or storage paths.
/// </summary>
/// <param name="Name">The stable configured service name.</param>
/// <param name="Type">The calendar provider type.</param>
/// <param name="IsDefault"><see langword="true"/> when the service is the configured default; otherwise, <see langword="false"/>.</param>
public sealed record CalendarServiceSummary(string Name, ServiceType Type, bool IsDefault);
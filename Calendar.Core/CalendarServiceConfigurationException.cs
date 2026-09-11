namespace Calendar.Core;

/// <summary>
/// Represents an invalid or unresolved calendar service configuration.
/// </summary>
public sealed class CalendarServiceConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarServiceConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the configuration failure.</param>
    public CalendarServiceConfigurationException(string message)
        : base(message)
    {
    }
}
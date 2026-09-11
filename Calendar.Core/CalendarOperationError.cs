namespace Calendar.Core;

/// <summary>
/// Identifies the category of a safe calendar operation error.
/// </summary>
public enum CalendarOperationErrorCategory
{
    /// <summary>The calendar provider could not complete the operation.</summary>
    Provider,

    /// <summary>The calendar provider configuration is invalid.</summary>
    Configuration
}

/// <summary>
/// Describes a calendar operation failure without exposing provider exception details.
/// </summary>
/// <param name="Category">The failure category.</param>
/// <param name="Message">The safe failure message.</param>
public sealed record CalendarOperationError(
    CalendarOperationErrorCategory Category,
    string Message);
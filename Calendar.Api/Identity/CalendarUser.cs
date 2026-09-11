using Microsoft.AspNetCore.Identity;

namespace Calendar.Api.Identity;

/// <summary>
/// Represents a local family account.
/// </summary>
public sealed class CalendarUser : IdentityUser
{
    /// <summary>Gets or sets the family member display name.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Gets or sets a value that indicates whether the account may sign in.</summary>
    public bool IsActive { get; set; } = true;
}
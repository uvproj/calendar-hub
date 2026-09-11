namespace Calendar.Api.Contracts;

/// <summary>
/// Supplies a new family member account.
/// </summary>
/// <param name="Username">The local account username.</param>
/// <param name="DisplayName">The family member display name.</param>
/// <param name="Passcode">The initial account passcode.</param>
/// <param name="IsAdministrator">Whether the account can manage members.</param>
public sealed record CreateMemberRequest(
    string Username,
    string DisplayName,
    string Passcode,
    bool IsAdministrator = false);

/// <summary>
/// Supplies a member active-state change.
/// </summary>
/// <param name="IsActive">Whether the account may sign in.</param>
public sealed record SetMemberActiveRequest(bool IsActive);

/// <summary>
/// Supplies a replacement family member passcode.
/// </summary>
/// <param name="Passcode">The replacement passcode.</param>
public sealed record ResetPasscodeRequest(string Passcode);

/// <summary>
/// Describes a family member account without authentication data.
/// </summary>
/// <param name="Id">The local account identifier.</param>
/// <param name="Username">The local account username.</param>
/// <param name="DisplayName">The family member display name.</param>
/// <param name="IsActive">Whether the account may sign in.</param>
/// <param name="IsAdministrator">Whether the account can manage members.</param>
public sealed record MemberResponse(
    string Id,
    string Username,
    string DisplayName,
    bool IsActive,
    bool IsAdministrator);
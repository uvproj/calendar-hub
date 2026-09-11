namespace Calendar.Api.Contracts;

/// <summary>
/// Supplies the antiforgery token required by state-changing requests.
/// </summary>
/// <param name="RequestToken">The token value to send in the indicated header.</param>
/// <param name="HeaderName">The antiforgery request header name.</param>
public sealed record AntiforgeryTokenResponse(string RequestToken, string HeaderName);

/// <summary>
/// Supplies the first administrator account and local bootstrap secret.
/// </summary>
/// <param name="Username">The administrator username.</param>
/// <param name="DisplayName">The family member display name.</param>
/// <param name="Passcode">The administrator passcode.</param>
/// <param name="BootstrapSecret">The one-time secret configured on the host.</param>
public sealed record BootstrapRequest(
    string Username,
    string DisplayName,
    string Passcode,
    string BootstrapSecret);

/// <summary>
/// Supplies local account credentials.
/// </summary>
/// <param name="Username">The local account username.</param>
/// <param name="Passcode">The local account passcode.</param>
public sealed record LoginRequest(string Username, string Passcode);

/// <summary>
/// Describes the signed-in family member.
/// </summary>
/// <param name="Id">The local account identifier.</param>
/// <param name="Username">The local account username.</param>
/// <param name="DisplayName">The family member display name.</param>
/// <param name="IsAdministrator">Whether the account can manage members.</param>
public sealed record CurrentUserResponse(
    string Id,
    string Username,
    string DisplayName,
    bool IsAdministrator);
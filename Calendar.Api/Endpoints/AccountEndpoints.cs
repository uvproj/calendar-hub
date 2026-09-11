using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Calendar.Api.Contracts;
using Calendar.Api.Data;
using Calendar.Api.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Calendar.Api.Endpoints;

internal static class AccountEndpoints
{
    internal const string AdministratorRole = "Administrator";
    private const int MinimumPasscodeLength = 8;
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);

    internal static WebApplication MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Accounts");

        group.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return TypedResults.Ok(new AntiforgeryTokenResponse(
                    tokens.RequestToken ?? string.Empty,
                    tokens.HeaderName ?? "X-CSRF-TOKEN"));
            })
            .AllowAnonymous()
            .WithSummary("Issues an antiforgery token.")
            .WithDescription("Sets the same-origin antiforgery cookie and returns the request header token.");

        group.MapPost("/bootstrap", BootstrapAsync)
            .AllowAnonymous()
            .RequireAntiforgery()
            .RequireRateLimiting("authentication")
            .WithSummary("Creates the first administrator.")
            .WithDescription("Creates one administrator only when no accounts exist and the configured bootstrap secret matches.")
            .Produces<CurrentUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireAntiforgery()
            .RequireRateLimiting("authentication")
            .WithSummary("Signs in a family member.")
            .WithDescription("Validates a local passcode and creates a secure same-origin cookie session.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", async (SignInManager<CalendarUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.NoContent();
            })
            .RequireAuthorization()
            .RequireAntiforgery()
            .WithSummary("Signs out the current family member.")
            .WithDescription("Clears the local authentication cookie.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", CurrentUserAsync)
            .RequireAuthorization()
            .WithSummary("Gets the current family member.")
            .WithDescription("Returns safe profile and administrator-role information for the cookie session.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> BootstrapAsync(
        BootstrapRequest request,
        IConfiguration configuration,
        CalendarIdentityDbContext database,
        UserManager<CalendarUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<CalendarUser> signInManager,
        CancellationToken cancellationToken)
    {
        var validationProblem = ValidateCredentials(request.Username, request.DisplayName, request.Passcode);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var configuredSecret = configuration["Bootstrap:Secret"];
        if (string.IsNullOrWhiteSpace(configuredSecret) ||
            !SecretsMatch(configuredSecret, request.BootstrapSecret))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Bootstrap is not available.");
        }

        await BootstrapLock.WaitAsync(cancellationToken);
        try
        {
            if (await database.Users.AnyAsync(cancellationToken))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Bootstrap has already completed.");
            }

            if (!await roleManager.RoleExistsAsync(AdministratorRole))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(AdministratorRole));
                if (!roleResult.Succeeded)
                {
                    return AccountOperationFailed();
                }
            }

            var user = new CalendarUser
            {
                UserName = request.Username.Trim(),
                DisplayName = request.DisplayName.Trim(),
                IsActive = true
            };
            var createResult = await userManager.CreateAsync(user, request.Passcode);
            if (!createResult.Succeeded)
            {
                return AccountOperationFailed();
            }

            var roleAssignmentResult = await userManager.AddToRoleAsync(user, AdministratorRole);
            if (!roleAssignmentResult.Succeeded)
            {
                await userManager.DeleteAsync(user);
                return AccountOperationFailed();
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return TypedResults.Created(
                "/api/auth/me",
                new CurrentUserResponse(user.Id, user.UserName!, user.DisplayName, true));
        }
        finally
        {
            BootstrapLock.Release();
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<CalendarUser> userManager,
        SignInManager<CalendarUser> signInManager)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Passcode))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Username and passcode are required.");
        }

        var user = await userManager.FindByNameAsync(request.Username.Trim());
        if (user is null || !user.IsActive)
        {
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Passcode,
            isPersistent: false,
            lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Sign-in is temporarily unavailable.");
        }

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        var isAdministrator = await userManager.IsInRoleAsync(user, AdministratorRole);
        return TypedResults.Ok(new CurrentUserResponse(
            user.Id,
            user.UserName!,
            user.DisplayName,
            isAdministrator));
    }

    private static async Task<IResult> CurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<CalendarUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new CurrentUserResponse(
            user.Id,
            user.UserName!,
            user.DisplayName,
            await userManager.IsInRoleAsync(user, AdministratorRole)));
    }

    internal static IResult? ValidateCredentials(string username, string displayName, string passcode)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 64)
        {
            errors[nameof(username)] = ["Username is required and cannot exceed 64 characters."];
        }
        else if (username.Trim().Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            errors[nameof(username)] = ["Username contains unsupported characters."];
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100)
        {
            errors[nameof(displayName)] = ["Display name is required and cannot exceed 100 characters."];
        }

        if (string.IsNullOrEmpty(passcode) || passcode.Length < MinimumPasscodeLength || passcode.Length > 128)
        {
            errors[nameof(passcode)] = [$"Passcode must contain between {MinimumPasscodeLength} and 128 characters."];
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }

    private static bool SecretsMatch(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied ?? string.Empty);
        return expectedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static IResult InvalidCredentials() => TypedResults.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "The username or passcode is invalid.");

    internal static IResult AccountOperationFailed() => TypedResults.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "The account operation could not be completed.");
}
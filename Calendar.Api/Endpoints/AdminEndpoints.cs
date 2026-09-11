using Calendar.Api.Contracts;
using Calendar.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Calendar.Api.Endpoints;

internal static class AdminEndpoints
{
    internal static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/members")
            .RequireAuthorization(policy => policy.RequireRole(AccountEndpoints.AdministratorRole))
            .WithTags("Administration");

        group.MapGet("/", ListMembersAsync)
            .WithSummary("Lists family members.")
            .WithDescription("Returns safe local account details to administrators.")
            .Produces<IReadOnlyList<MemberResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/", CreateMemberAsync)
            .RequireAntiforgery()
            .WithSummary("Creates a family member.")
            .WithDescription("Creates an active local account without exposing password data.")
            .Produces<MemberResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{memberId}/active", SetActiveAsync)
            .RequireAntiforgery()
            .WithSummary("Changes member access.")
            .WithDescription("Enables or disables a family member while preserving at least one active administrator.")
            .Produces<MemberResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{memberId}/passcode", ResetPasscodeAsync)
            .RequireAntiforgery()
            .WithSummary("Resets a member passcode.")
            .WithDescription("Replaces the passcode and invalidates existing sessions for the member.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListMembersAsync(
        UserManager<CalendarUser> userManager,
        CancellationToken cancellationToken)
    {
        var users = await userManager.Users.OrderBy(user => user.UserName).ToListAsync(cancellationToken);
        var members = new List<MemberResponse>(users.Count);
        foreach (var user in users)
        {
            members.Add(await ToResponseAsync(user, userManager));
        }

        return TypedResults.Ok<IReadOnlyList<MemberResponse>>(members);
    }

    private static async Task<IResult> CreateMemberAsync(
        CreateMemberRequest request,
        UserManager<CalendarUser> userManager)
    {
        var validationProblem = AccountEndpoints.ValidateCredentials(
            request.Username,
            request.DisplayName,
            request.Passcode);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var user = new CalendarUser
        {
            UserName = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, request.Passcode);
        if (!result.Succeeded)
        {
            return AccountEndpoints.AccountOperationFailed();
        }

        if (request.IsAdministrator)
        {
            var roleResult = await userManager.AddToRoleAsync(user, AccountEndpoints.AdministratorRole);
            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(user);
                return AccountEndpoints.AccountOperationFailed();
            }
        }

        var response = await ToResponseAsync(user, userManager);
        return TypedResults.Created($"/api/admin/members/{user.Id}", response);
    }

    private static async Task<IResult> SetActiveAsync(
        string memberId,
        SetMemberActiveRequest request,
        UserManager<CalendarUser> userManager)
    {
        var user = await userManager.FindByIdAsync(memberId);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (!request.IsActive && user.IsActive &&
            await userManager.IsInRoleAsync(user, AccountEndpoints.AdministratorRole))
        {
            var administrators = await userManager.GetUsersInRoleAsync(AccountEndpoints.AdministratorRole);
            if (administrators.Count(candidate => candidate.IsActive) <= 1)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "The last active administrator cannot be disabled.");
            }
        }

        user.IsActive = request.IsActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return AccountEndpoints.AccountOperationFailed();
        }

        if (!request.IsActive)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }

        return TypedResults.Ok(await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> ResetPasscodeAsync(
        string memberId,
        ResetPasscodeRequest request,
        UserManager<CalendarUser> userManager)
    {
        if (string.IsNullOrEmpty(request.Passcode) || request.Passcode.Length is < 8 or > 128)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Passcode)] = ["Passcode must contain between 8 and 128 characters."]
            });
        }

        var user = await userManager.FindByIdAsync(memberId);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.Passcode);
        if (!result.Succeeded)
        {
            return AccountEndpoints.AccountOperationFailed();
        }

        await userManager.UpdateSecurityStampAsync(user);
        return TypedResults.NoContent();
    }

    private static async Task<MemberResponse> ToResponseAsync(
        CalendarUser user,
        UserManager<CalendarUser> userManager) =>
        new(
            user.Id,
            user.UserName!,
            user.DisplayName,
            user.IsActive,
            await userManager.IsInRoleAsync(user, AccountEndpoints.AdministratorRole));
}
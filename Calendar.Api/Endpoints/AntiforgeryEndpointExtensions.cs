using Microsoft.AspNetCore.Antiforgery;

namespace Calendar.Api.Endpoints;

internal static class AntiforgeryEndpointExtensions
{
    internal static RouteHandlerBuilder RequireAntiforgery(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (invocationContext, next) =>
        {
            var antiforgery = invocationContext.HttpContext.RequestServices
                .GetRequiredService<IAntiforgery>();

            try
            {
                await antiforgery.ValidateRequestAsync(invocationContext.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "The antiforgery token is invalid or missing.");
            }

            return await next(invocationContext);
        });
    }
}
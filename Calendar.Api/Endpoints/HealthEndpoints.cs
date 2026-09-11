using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Calendar.Api.Endpoints;

internal static class HealthEndpoints
{
    internal static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        })
        .AllowAnonymous()
        .WithSummary("Reports whether the API process is running.")
        .WithDescription("Returns a lightweight liveness result without checking dependencies.");

        app.MapHealthChecks("/health/ready")
            .AllowAnonymous()
            .WithSummary("Reports whether the API dependencies are ready.")
            .WithDescription("Returns readiness for the local API dependencies.");

        return app;
    }
}
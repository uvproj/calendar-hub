using System.Threading.RateLimiting;
using Calendar.Api.Ai;
using Calendar.Api.Data;
using Calendar.Api.Endpoints;
using Calendar.Api.Identity;
using Calendar.Api.Services;
using Calendar.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);
var dataDirectory = builder.Configuration["CalendarHub:DataDirectory"];
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "calendar-hub");
}

Directory.CreateDirectory(dataDirectory);
var databasePath = builder.Configuration["CalendarHub:DatabasePath"];
if (string.IsNullOrWhiteSpace(databasePath))
{
    databasePath = Path.Combine(dataDirectory, "calendar-hub.db");
}

var servicesPath = builder.Configuration["CalendarHub:ServicesPath"];

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "calendar-hub-antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 120,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddFixedWindowLimiter("authentication", limiterOptions =>
    {
        limiterOptions.AutoReplenishment = true;
        limiterOptions.PermitLimit = 10;
        limiterOptions.QueueLimit = 0;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("calendar-interpretation", limiterOptions =>
    {
        limiterOptions.AutoReplenishment = true;
        limiterOptions.PermitLimit = 5;
        limiterOptions.QueueLimit = 0;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
});
builder.Services.AddDbContext<CalendarIdentityDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath};Pooling=False"));
builder.Services
    .AddIdentityCore<CalendarUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<CalendarIdentityDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "calendar-hub-auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
var timeZoneId = builder.Configuration["CalendarHub:TimeZoneId"] ?? "UTC";
builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
builder.Services.AddSingleton(string.IsNullOrWhiteSpace(servicesPath)
    ? new ServiceStore()
    : new ServiceStore(servicesPath));
builder.Services.AddSingleton<CalendarServiceFactory>();
builder.Services.AddSingleton<CalendarAggregationService>();
builder.Services.AddSingleton<CalendarBatchCreationService>();
builder.Services.AddSingleton<ICalendarApiService, CalendarApiService>();
builder.Services.AddOptions<CalendarAiOptions>()
    .Bind(builder.Configuration.GetSection("CalendarAi"));
builder.Services.AddSingleton<CalendarTextInterpretationParser>();
builder.Services.AddSingleton<ICalendarTextInterpreter>(services =>
{
    var environment = services.GetRequiredService<IHostEnvironment>();
    var options = services.GetRequiredService<IOptions<CalendarAiOptions>>().Value;
    if (environment.IsEnvironment("Testing") ||
        string.Equals(options.Provider, "Fake", StringComparison.OrdinalIgnoreCase))
    {
        return new FakeCalendarTextInterpreter();
    }

    if (!string.Equals(options.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
    {
        return new UnavailableCalendarTextInterpreter("configuration_error");
    }

    if (string.IsNullOrWhiteSpace(options.ApiKey) ||
        string.IsNullOrWhiteSpace(options.Model) ||
        !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var endpoint))
    {
        return new UnavailableCalendarTextInterpreter("configuration_error");
    }

    var openAiClient = new OpenAIClient(
        new ApiKeyCredential(options.ApiKey),
        new OpenAIClientOptions { Endpoint = endpoint });
    IChatClient chatClient = openAiClient.GetChatClient(options.Model).AsIChatClient();
    return new OpenAiCalendarTextInterpreter(
        chatClient,
        services.GetRequiredService<CalendarTextInterpretationParser>(),
        services.GetRequiredService<ILogger<OpenAiCalendarTextInterpreter>>());
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) &&
        context.Request.Path.Equals("/api/calendar-interpretations"))
    {
        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize = CalendarInterpretationEndpoints.MaxRequestBodySize;
        }
    }

    await next(context);
});

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<CalendarIdentityDbContext>().Database;
    database.EnsureCreated();
}

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

var webRootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var webIndexPath = Path.Combine(webRootPath, "index.html");
if (app.Environment.IsProduction() && File.Exists(webIndexPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapOpenApi().RequireAuthorization();
app.MapHealthEndpoints();
app.MapAccountEndpoints();
app.MapAdminEndpoints();
app.MapCalendarEndpoints();
app.MapCalendarInterpretationEndpoints();

if (app.Environment.IsProduction() && File.Exists(webIndexPath))
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.Run();

/// <summary>
/// Provides the entry point marker used by integration tests.
/// </summary>
public partial class Program;

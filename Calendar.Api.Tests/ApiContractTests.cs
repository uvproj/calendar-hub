using System.Net;
using Calendar.Api.Ai;
using Calendar.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Calendar.Api.Tests;

public sealed class ApiContractTests : IClassFixture<CalendarApiFactory>
{
    private readonly HttpClient _client;

    public ApiContractTests(CalendarApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task HealthLive_AnonymousRequest_ReturnsOk()
    {
        using var response = await _client.GetAsync("/health/live", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CalendarSummaries_AnonymousRequest_ReturnsUnauthorized()
    {
        using var response = await _client.GetAsync("/api/calendars", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class CalendarApiFactory : WebApplicationFactory<Program>
{
    private readonly ICalendarApiService? _calendarService;
    private readonly ICalendarTextInterpreter? _textInterpreter;
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "calendar-hub-tests",
        Guid.NewGuid().ToString("N"));

    public CalendarApiFactory()
    {
    }

    internal CalendarApiFactory(
        ICalendarApiService calendarService,
        ICalendarTextInterpreter? textInterpreter = null)
    {
        _calendarService = calendarService;
        _textInterpreter = textInterpreter;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("CalendarHub:DataDirectory", _dataDirectory);
        builder.UseSetting("Bootstrap:Secret", "integration-bootstrap-secret");
        if (_calendarService is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICalendarApiService>();
                services.AddSingleton(_calendarService);
                if (_textInterpreter is not null)
                {
                    services.RemoveAll<ICalendarTextInterpreter>();
                    services.AddSingleton(_textInterpreter);
                }
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}
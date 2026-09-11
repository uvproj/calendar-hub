using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Calendar.Api.Ai;
using Calendar.Api.Services;
using Calendar.Core;

namespace Calendar.Api.Tests;

public sealed class CalendarIntegrationTests
{
    [Fact]
    public async Task Interpret_AnonymousRequest_ReturnsUnauthorized()
    {
        using var factory = new CalendarApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var response = await client.PostAsJsonAsync("/api/calendar-interpretations", new
        {
            text = "Dinner tomorrow at 6pm",
            destinationCalendar = "Family"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Interpret_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        var interpreter = new TrackingTextInterpreter();
        using var factory = new CalendarApiFactory(new FakeCalendarApiService(), interpreter);
        using var client = await CreateAuthenticatedClientAsync(factory);

        using var response = await client.PostAsJsonAsync("/api/calendar-interpretations", new
        {
            text = "Dinner tomorrow at 6pm",
            destinationCalendar = "Family"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Interpret_UnknownDestination_DoesNotInvokeInterpreter()
    {
        var interpreter = new TrackingTextInterpreter();
        using var factory = new CalendarApiFactory(new FakeCalendarApiService(), interpreter);
        using var client = await CreateAuthenticatedClientAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateInterpretationRequest(token, "Dinner tomorrow at 6pm", "Unknown");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, interpreter.CallCount);
    }

    [Fact]
    public async Task Interpret_MultipleDrafts_ReturnsPreviewWithoutCalendarMutation()
    {
        var calendarService = new FakeCalendarApiService();
        var interpreter = new TrackingTextInterpreter();
        using var factory = new CalendarApiFactory(calendarService, interpreter);
        using var client = await CreateAuthenticatedClientAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateInterpretationRequest(
            token,
            "Dinner Friday at 6pm and picnic Saturday all day",
            "Family");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dinner", body, StringComparison.Ordinal);
        Assert.Contains("Picnic", body, StringComparison.Ordinal);
        Assert.Equal(1, interpreter.CallCount);
        Assert.Equal(0, calendarService.BatchCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Interpret_BlankText_DoesNotInvokeInterpreter(string text)
    {
        var interpreter = new TrackingTextInterpreter();
        using var factory = new CalendarApiFactory(new FakeCalendarApiService(), interpreter);
        using var client = await CreateAuthenticatedClientAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateInterpretationRequest(token, text, "Family");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, interpreter.CallCount);
    }

    [Fact]
    public async Task Interpret_OversizedText_DoesNotInvokeInterpreter()
    {
        var interpreter = new TrackingTextInterpreter();
        using var factory = new CalendarApiFactory(new FakeCalendarApiService(), interpreter);
        using var client = await CreateAuthenticatedClientAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateInterpretationRequest(token, new string('x', 4001), "Family");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, interpreter.CallCount);
    }

    [Fact]
    public async Task Interpret_ProviderFailure_ReturnsSafeServiceUnavailableResponse()
    {
        var interpreter = new TrackingTextInterpreter(new CalendarTextInterpretationResult(
            [],
            [],
            [],
            new CalendarTextInterpretationFailure(
                "provider_unavailable",
                "Calendar text interpretation is temporarily unavailable.")));
        using var factory = new CalendarApiFactory(new FakeCalendarApiService(), interpreter);
        using var client = await CreateAuthenticatedClientAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateInterpretationRequest(token, "raw-prompt-secret", "Family");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("raw-prompt-secret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-provider-secret", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Interpret_TestingEnvironmentAutomaticallyUsesFakeProvider()
    {
        using var factory = new CalendarApiFactory(new FakeCalendarApiService());
        using var client = await CreateAuthenticatedClientAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateInterpretationRequest(token, "sample: dinner-and-picnic", "Family");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("deterministic fake provider", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Calendars_AuthenticatedRequest_ReturnsSafeSummaries()
    {
        var fake = new FakeCalendarApiService();
        using var factory = new CalendarApiFactory(fake);
        using var client = await CreateAuthenticatedClientAsync(factory);

        using var response = await client.GetAsync("/api/calendars");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("C:\\private\\calendar.json", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Events_RangeExceedsLimit_ReturnsBadRequest()
    {
        using var factory = new CalendarApiFactory(new FakeCalendarApiService());
        using var client = await CreateAuthenticatedClientAsync(factory);

        using var response = await client.GetAsync(
            "/api/events?start=2026-01-01T00:00:00Z&end=2027-01-03T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Events_PartialProviderResult_MapsSourceAndSafeError()
    {
        using var factory = new CalendarApiFactory(new FakeCalendarApiService());
        using var client = await CreateAuthenticatedClientAsync(factory);

        using var response = await client.GetAsync(
            "/api/events?start=2026-09-01T00:00:00Z&end=2026-10-01T00:00:00Z&calendar=Family&calendar=Work");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Family:event-1", body, StringComparison.Ordinal);
        Assert.Contains("Calendar provider operation failed.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-provider-secret", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Batch_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var factory = new CalendarApiFactory(new FakeCalendarApiService());
        using var client = await CreateAuthenticatedClientAsync(factory);

        using var response = await client.PostAsJsonAsync("/api/events/batch", ValidBatch());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Batch_ExplicitDestination_ReturnsOrderedPartialOutcomes()
    {
        var fake = new FakeCalendarApiService();
        using var factory = new CalendarApiFactory(fake);
        using var client = await CreateAuthenticatedClientAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events/batch")
        {
            Content = JsonContent.Create(ValidBatch())
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Family", fake.LastDestination);
        Assert.Contains("Calendar provider operation failed.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Batch_InvalidAllDayBoundary_DoesNotInvokeCoreService()
    {
        var fake = new FakeCalendarApiService();
        using var factory = new CalendarApiFactory(fake);
        using var client = await CreateAuthenticatedClientAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events/batch")
        {
            Content = JsonContent.Create(new
            {
                destinationCalendar = "Family",
                events = new[]
                {
                    new
                    {
                        name = "Invalid all day",
                        startsAt = "2026-09-12T01:00:00Z",
                        endsAt = "2026-09-13T00:00:00Z",
                        isAllDay = true,
                        invitees = Array.Empty<string>()
                    }
                }
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.LastDestination);
    }

    private static object ValidBatch() => new
    {
        destinationCalendar = "Family",
        events = new[]
        {
            new
            {
                name = "Dinner",
                description = (string?)"Together",
                location = (string?)"Home",
                startsAt = "2026-09-12T18:00:00+01:00",
                endsAt = "2026-09-12T19:00:00+01:00",
                isAllDay = false,
                invitees = new[] { "family@example.com" }
            },
            new
            {
                name = "Provider failure",
                description = (string?)null,
                location = (string?)null,
                startsAt = "2026-09-13T18:00:00+01:00",
                endsAt = "2026-09-13T19:00:00+01:00",
                isAllDay = false,
                invitees = Array.Empty<string>()
            }
        }
    };

    private static HttpRequestMessage CreateInterpretationRequest(
        string token,
        string text,
        string destinationCalendar)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/calendar-interpretations")
        {
            Content = JsonContent.Create(new { text, destinationCalendar })
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return request;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(CalendarApiFactory factory)
    {
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var token = await GetAntiforgeryTokenAsync(client);
        using var bootstrapRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/bootstrap")
        {
            Content = JsonContent.Create(new
            {
                username = "admin",
                displayName = "Family Admin",
                passcode = "family-admin-passcode",
                bootstrapSecret = "integration-bootstrap-secret"
            })
        };
        bootstrapRequest.Headers.Add("X-CSRF-TOKEN", token);
        using var bootstrapResponse = await client.SendAsync(bootstrapRequest);
        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);

        token = await GetAntiforgeryTokenAsync(client);
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                username = "admin",
                passcode = "family-admin-passcode"
            })
        };
        loginRequest.Headers.Add("X-CSRF-TOKEN", token);
        using var loginResponse = await client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return client;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery");
        return response.GetProperty("requestToken").GetString()!;
    }

    private sealed class FakeCalendarApiService : ICalendarApiService
    {
        public string? LastDestination { get; private set; }

        public int BatchCallCount { get; private set; }

        public IReadOnlyList<CalendarServiceSummary> ListSummaries() =>
            [new CalendarServiceSummary("Family", ServiceType.FileSystem, true)];

        public Task<CalendarAggregationResult> QueryAsync(
            DateTimeOffset start,
            DateTimeOffset end,
            IReadOnlyList<string>? calendarNames,
            CancellationToken cancellationToken)
        {
            var calendarEvent = CreateEvent("event-1", "Dinner", start.AddHours(1), start.AddHours(2));
            return Task.FromResult(new CalendarAggregationResult(
                [new SourceCalendarEvent(
                    "Family",
                    ServiceType.FileSystem,
                    new CalendarEventIdentity("Family", "Family:event-1"),
                    calendarEvent)],
                [new CalendarQueryError(
                    "Work",
                    ServiceType.Google,
                    new CalendarOperationError(
                        CalendarOperationErrorCategory.Provider,
                        "Calendar provider operation failed."))]));
        }

        public Task<CalendarBatchCreationResult> CreateBatchAsync(
            string destinationCalendar,
            IReadOnlyList<CalendarEventCreateRequest> drafts,
            CancellationToken cancellationToken)
        {
            BatchCallCount++;
            LastDestination = destinationCalendar;
            return Task.FromResult(new CalendarBatchCreationResult(
                destinationCalendar,
                ServiceType.FileSystem,
                [
                    new CalendarBatchCreationItemResult(
                        0,
                        CreateEvent("created-1", drafts[0].Name, drafts[0].StartsAt, drafts[0].EndsAt),
                        null),
                    new CalendarBatchCreationItemResult(
                        1,
                        null,
                        new CalendarOperationError(
                            CalendarOperationErrorCategory.Provider,
                            "Calendar provider operation failed."))
                ]));
        }

        private static CalendarEvent CreateEvent(
            string id,
            string name,
            DateTimeOffset startsAt,
            DateTimeOffset endsAt) =>
            new(id, name, null, null, startsAt, endsAt, false, []);
    }

    private sealed class TrackingTextInterpreter : ICalendarTextInterpreter
    {
        private readonly CalendarTextInterpretationResult? _result;

        internal TrackingTextInterpreter(CalendarTextInterpretationResult? result = null)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<CalendarTextInterpretationResult> InterpretAsync(
            CalendarTextInterpretationInput input,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (_result is not null)
            {
                return Task.FromResult(_result);
            }

            IReadOnlyList<CalendarEventCreateRequest> drafts =
            [
                new(
                    "Dinner",
                    null,
                    "Home",
                    new DateTimeOffset(2026, 9, 18, 18, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 9, 18, 19, 0, 0, TimeSpan.Zero),
                    false,
                    []),
                new(
                    "Picnic",
                    null,
                    null,
                    new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero),
                    true,
                    [])
            ];
            return Task.FromResult(new CalendarTextInterpretationResult(drafts, [], []));
        }
    }
}
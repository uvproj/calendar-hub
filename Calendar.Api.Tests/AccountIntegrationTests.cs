using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Calendar.Api.Tests;

public sealed class AccountIntegrationTests
{
    private const string AdministratorPasscode = "family-admin-passcode";

    [Fact]
    public async Task Bootstrap_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var factory = new CalendarApiFactory();
        using var client = CreateClient(factory);
        using var response = await client.PostAsJsonAsync("/api/auth/bootstrap", new
        {
            username = "admin",
            displayName = "Family Admin",
            passcode = AdministratorPasscode,
            bootstrapSecret = "integration-bootstrap-secret"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_WithConfiguredSecret_WorksOnlyOnce()
    {
        using var factory = new CalendarApiFactory();
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        using var firstResponse = await PostWithTokenAsync(client, "/api/auth/bootstrap", new
        {
            username = "admin",
            displayName = "Family Admin",
            passcode = AdministratorPasscode,
            bootstrapSecret = "integration-bootstrap-secret"
        }, token);
        using var currentUserResponse = await client.GetAsync("/api/auth/me");
        var authenticatedToken = await GetAntiforgeryTokenAsync(client);
        using var secondResponse = await PostWithTokenAsync(client, "/api/auth/bootstrap", new
        {
            username = "other-admin",
            displayName = "Other Admin",
            passcode = AdministratorPasscode,
            bootstrapSecret = "integration-bootstrap-secret"
        }, authenticatedToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ProvidesCurrentUser()
    {
        using var factory = new CalendarApiFactory();
        using var client = CreateClient(factory);
        await EnsureAdministratorAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var loginResponse = await PostWithTokenAsync(client, "/api/auth/login", new
        {
            username = "admin",
            passcode = AdministratorPasscode
        }, token);
        using var currentUserResponse = await client.GetAsync("/api/auth/me");
        var currentUser = await currentUserResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal("admin", currentUser.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Login_RepeatedInvalidCredentials_LocksAccount()
    {
        using var factory = new CalendarApiFactory();
        using var client = CreateClient(factory);
        await EnsureAdministratorAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        HttpResponseMessage? finalResponse = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            finalResponse?.Dispose();
            finalResponse = await PostWithTokenAsync(client, "/api/auth/login", new
            {
                username = "admin",
                passcode = "incorrect-passcode"
            }, token);
        }

        using (finalResponse)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, finalResponse!.StatusCode);
        }
    }

    [Fact]
    public async Task Admin_MemberLifecycle_IsAuthorizedAndDoesNotExposePasscodes()
    {
        using var factory = new CalendarApiFactory();
        using var administratorClient = CreateClient(factory);
        await EnsureAdministratorAsync(administratorClient);
        await LoginAsync(administratorClient, "admin", AdministratorPasscode);
        var token = await GetAntiforgeryTokenAsync(administratorClient);
        using var createResponse = await PostWithTokenAsync(administratorClient, "/api/admin/members", new
        {
            username = "member",
            displayName = "Family Member",
            passcode = "member-passcode",
            isAdministrator = false
        }, token);
        var member = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var memberId = member.GetProperty("id").GetString();
        using var listResponse = await administratorClient.GetAsync("/api/admin/members");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        using var disableResponse = await PutWithTokenAsync(
            administratorClient,
            $"/api/admin/members/{memberId}/active",
            new { isActive = false },
            token);
        using var resetResponse = await PutWithTokenAsync(
            administratorClient,
            $"/api/admin/members/{memberId}/passcode",
            new { passcode = "replacement-passcode" },
            token);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.DoesNotContain("member-passcode", listBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_NonAdministrator_ReturnsForbidden()
    {
        using var factory = new CalendarApiFactory();
        using var administratorClient = CreateClient(factory);
        await EnsureAdministratorAsync(administratorClient);
        await LoginAsync(administratorClient, "admin", AdministratorPasscode);
        var administratorToken = await GetAntiforgeryTokenAsync(administratorClient);
        using var createResponse = await PostWithTokenAsync(administratorClient, "/api/admin/members", new
        {
            username = "ordinary-member",
            displayName = "Ordinary Member",
            passcode = "ordinary-passcode"
        }, administratorToken);

        using var memberClient = CreateClient(factory);
        await LoginAsync(memberClient, "ordinary-member", "ordinary-passcode");
        using var response = await memberClient.GetAsync("/api/admin/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpClient CreateClient(CalendarApiFactory factory) => factory.CreateClient(new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static async Task EnsureAdministratorAsync(HttpClient client)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        using var response = await PostWithTokenAsync(client, "/api/auth/bootstrap", new
        {
            username = "admin",
            displayName = "Family Admin",
            passcode = AdministratorPasscode,
            bootstrapSecret = "integration-bootstrap-secret"
        }, token);
        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected bootstrap status: {response.StatusCode}");
    }

    private static async Task LoginAsync(HttpClient client, string username, string passcode)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        using var response = await PostWithTokenAsync(client, "/api/auth/login", new
        {
            username,
            passcode
        }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery");
        return response.GetProperty("requestToken").GetString()!;
    }

    private static Task<HttpResponseMessage> PostWithTokenAsync(
        HttpClient client,
        string requestUri,
        object body,
        string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PutWithTokenAsync(
        HttpClient client,
        string requestUri,
        object body,
        string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }
}
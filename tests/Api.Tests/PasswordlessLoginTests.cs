using System.Net;
using System.Net.Http.Json;
using Identity.Contracts;
using Identity.Implementation;
using FreizeitCockpit.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests;

public sealed class PasswordlessLoginTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public PasswordlessLoginTests(WebApplicationFactory<Program> factory)
    {
        client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender, NoOpSender>();
            });
        })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public async Task RequestingCodeNeverRevealsWhetherAddressIsRegistered()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var knownRequest = CreateJsonPost(
            "/api/v1/auth/code",
            new { email = "miriam@example.test" }, antiforgery);
        using var knownResponse = await client.SendAsync(knownRequest, cancellationToken);
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var unknownRequest = CreateJsonPost(
            "/api/v1/auth/code",
            new { email = "unbekannt@example.test" }, antiforgery);
        using var unknownResponse = await client.SendAsync(unknownRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);

        var known = await knownResponse.Content.ReadFromJsonAsync<CodeRequestResponse>(cancellationToken);
        var unknown = await unknownResponse.Content.ReadFromJsonAsync<CodeRequestResponse>(cancellationToken);
        Assert.Equal("Wenn die Adresse registriert ist, wurde ein Anmeldecode versendet.", known?.Message);
        Assert.Equal(known?.Message, unknown?.Message);
    }

    [Fact]
    public async Task LoginMutationsRejectRequestsWithoutAntiforgeryToken()
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/code",
            new { email = "miriam@example.test" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidCodeCreatesSecureRevocableSessionCookie()
    {
        var sender = new CapturingSender();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
            });
        });
        using var sessionClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        var antiforgeryToken = await GetAntiforgeryAsync(sessionClient, cancellationToken);
        using var codeRequest = CreateJsonPost(
            "/api/v1/auth/code",
            new { email = "miriam@example.test" }, antiforgeryToken);
        using var codeResponse = await sessionClient.SendAsync(codeRequest, cancellationToken);
        var code = Assert.Single(sender.Codes);

        antiforgeryToken = await GetAntiforgeryAsync(sessionClient, cancellationToken);
        using var verifyRequest = CreateJsonPost(
            "/api/v1/auth/verify",
            new { email = "miriam@example.test", code, rememberMe = false }, antiforgeryToken);
        using var verifyResponse = await sessionClient.SendAsync(verifyRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, verifyResponse.StatusCode);
        var cookie = Assert.Single(verifyResponse.Headers.GetValues("Set-Cookie"));
        Assert.Contains("freizeit_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        using var sessionsResponse = await sessionClient.GetAsync(
            "/api/v1/auth/sessions",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<SessionResponse[]>(cancellationToken);
        var current = Assert.Single(sessions!);
        Assert.True(current.IsCurrent);

        using var antiforgeryResponse = await sessionClient.GetAsync(
            "/api/v1/auth/antiforgery",
            cancellationToken);
        var antiforgery = await antiforgeryResponse.Content
            .ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
        using var revokeRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/auth/sessions/{current.Id}");
        revokeRequest.Headers.Add("X-CSRF-TOKEN", antiforgery!.Token);
        using var revokeResponse = await sessionClient.SendAsync(revokeRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using var afterRevoke = await sessionClient.GetAsync(
            "/api/v1/auth/sessions",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    private sealed record CodeRequestResponse(string Message);

    private sealed record SessionResponse(Guid Id, bool IsCurrent);

    private sealed record AntiforgeryResponse(string Token);

    private static async Task<string> GetAntiforgeryAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/api/v1/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
        return Assert.IsType<string>(payload?.Token);
    }

    private static HttpRequestMessage CreateJsonPost(string uri, object body, string antiforgery)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);
        return request;
    }

    private sealed class CapturingSender : ILoginCodeSender
    {
        public List<string> Codes { get; } = [];

        public Task SendAsync(
            string email,
            string code,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Codes.Add(code);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpSender : ILoginCodeSender
    {
        public Task SendAsync(
            string email,
            string code,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

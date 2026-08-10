using System.Net;
using System.Net.Http.Json;
using Identity.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests;

public sealed class PasswordMaintenanceApiTests
{
    [Fact]
    public async Task ResetRequestIsGenericAndConfirmationUsesExplicitOutcomes()
    {
        var maintenance = new FakePasswordMaintenance();
        await using var factory = CreateFactory(maintenance);
        using var client = CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;
        var csrf = await GetAntiforgeryAsync(client, cancellationToken);

        using var request = CreateJsonRequest(
            "/api/v1/auth/password-reset/request",
            new { email = "unknown@example.test" },
            csrf);
        using var requested = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.Equal("unknown@example.test", maintenance.RequestedEmail);

        maintenance.ResetOutcome = PasswordResetOutcome.Invalid;
        using var confirm = CreateJsonRequest(
            "/api/v1/auth/password-reset/confirm",
            new { token = "invalid", newPassword = "Eine sichere neue Passphrase" },
            csrf);
        using var rejected = await client.SendAsync(confirm, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task ReauthenticationAndPasswordChangeUseAuthenticatedSession()
    {
        var maintenance = new FakePasswordMaintenance();
        await using var factory = CreateFactory(maintenance);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Authorization = new(
            "Test",
            "10000000-0000-0000-0000-000000000001");
        var cancellationToken = TestContext.Current.CancellationToken;
        var csrf = await GetAntiforgeryAsync(client, cancellationToken);

        using var reauthenticate = CreateJsonRequest(
            "/api/v1/auth/reauthenticate",
            new { password = "Eine sichere Testpassphrase" },
            csrf);
        using var reauthenticated = await client.SendAsync(reauthenticate, cancellationToken);
        using var change = CreateJsonRequest(
            "/api/v1/account/password",
            new
            {
                currentPassword = "Eine sichere Testpassphrase",
                newPassword = "Eine sichere neue Passphrase"
            },
            csrf);
        change.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        using var changed = await client.SendAsync(change, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, reauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            maintenance.Reauthentication?.UserId);
        Assert.Equal(
            Guid.Parse("90000000-0000-0000-0000-000000000001"),
            maintenance.PasswordChange?.SessionId);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IPasswordMaintenance maintenance) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordMaintenance>();
                services.AddSingleton(maintenance);
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<string> GetAntiforgeryAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
        return Assert.IsType<string>(value?.Token);
    }

    private static HttpRequestMessage CreateJsonRequest(string uri, object body, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return request;
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed class FakePasswordMaintenance : IPasswordMaintenance
    {
        public string? RequestedEmail { get; private set; }
        public PasswordResetOutcome ResetOutcome { get; set; } = PasswordResetOutcome.Succeeded;
        public PasswordChangeRequest? PasswordChange { get; private set; }
        public ReauthenticationRequest? Reauthentication { get; private set; }

        public Task RequestResetAsync(string email, CancellationToken cancellationToken)
        {
            RequestedEmail = email;
            return Task.CompletedTask;
        }

        public Task<PasswordResetOutcome> ConfirmResetAsync(
            PasswordResetConfirmation request,
            CancellationToken cancellationToken) => Task.FromResult(ResetOutcome);

        public Task<PasswordChangeOutcome> ChangePasswordAsync(
            PasswordChangeRequest request,
            CancellationToken cancellationToken)
        {
            PasswordChange = request;
            return Task.FromResult(PasswordChangeOutcome.Succeeded);
        }

        public Task<ReauthenticationOutcome> ReauthenticateAsync(
            ReauthenticationRequest request,
            CancellationToken cancellationToken)
        {
            Reauthentication = request;
            return Task.FromResult(ReauthenticationOutcome.Succeeded);
        }
    }
}

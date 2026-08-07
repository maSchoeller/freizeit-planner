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

public sealed class AccountLifecycleApiTests
{
    [Fact]
    public async Task AuthenticatedUserCanVerifyANewEmailWithAntiforgeryProtection()
    {
        var loginSender = new CapturingLoginCodeSender();
        var lifecycle = new EmailChangeLifecycleFake();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.RemoveAll<IEmailChangeLifecycle>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(loginSender);
                services.AddSingleton<IEmailChangeLifecycle>(lifecycle);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await LoginAsync(client, loginSender, cancellationToken);

        using var missingCsrf = await client.PostAsJsonAsync(
            "/api/v1/account/email-change",
            new { email = "neu@example.test" },
            cancellationToken);
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var request = CreateJsonPost(
            "/api/v1/account/email-change",
            new { email = "neu@example.test" },
            antiforgery);
        using var response = await client.SendAsync(request, cancellationToken);
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var confirm = CreateJsonPost(
            "/api/v1/account/email-change/confirm",
            new { email = "neu@example.test", code = "123456" },
            antiforgery);
        using var confirmed = await client.SendAsync(confirm, cancellationToken);
        var result = await confirmed.Content.ReadFromJsonAsync<EmailChangeResult>(cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(EmailChangeOutcome.Changed, result?.Outcome);
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), lifecycle.Request?.UserId);
        Assert.Equal("123456", lifecycle.Confirmation?.Code);
    }

    private static async Task LoginAsync(
        HttpClient client,
        CapturingLoginCodeSender sender,
        CancellationToken cancellationToken)
    {
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var requestCode = CreateJsonPost(
            "/api/v1/auth/code",
            new { email = "miriam@example.test" },
            antiforgery);
        using var requested = await client.SendAsync(requestCode, cancellationToken);
        requested.EnsureSuccessStatusCode();
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var verify = CreateJsonPost(
            "/api/v1/auth/verify",
            new { email = "miriam@example.test", code = Assert.Single(sender.Codes), rememberMe = false },
            antiforgery);
        using var verified = await client.SendAsync(verify, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
    }

    private static async Task<string> GetAntiforgeryAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
        return Assert.IsType<string>(value?.Token);
    }

    private static HttpRequestMessage CreateJsonPost(string uri, object body, string antiforgery)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);
        return request;
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed class CapturingLoginCodeSender : ILoginCodeSender
    {
        public List<string> Codes { get; } = [];

        public Task SendAsync(string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        {
            Codes.Add(code);
            return Task.CompletedTask;
        }
    }

    private sealed class EmailChangeLifecycleFake : IEmailChangeLifecycle
    {
        public EmailChangeRequest? Request { get; private set; }

        public ConfirmEmailChangeRequest? Confirmation { get; private set; }

        public Task RequestAsync(EmailChangeRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.CompletedTask;
        }

        public Task<EmailChangeResult> ConfirmAsync(
            ConfirmEmailChangeRequest request,
            CancellationToken cancellationToken)
        {
            Confirmation = request;
            return Task.FromResult(new EmailChangeResult(EmailChangeOutcome.Changed, request.Email));
        }
    }
}

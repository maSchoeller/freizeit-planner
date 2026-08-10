using System.Net;
using System.Net.Http.Json;
using FreizeitCockpit.TestSupport;
using Identity.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests;

public sealed class PasswordLoginTests
{
    [Fact]
    public async Task ValidPasswordReturnsAccessTokenAndSecureRefreshCookie()
    {
        var expiresAt = new DateTimeOffset(2026, 8, 10, 12, 15, 0, TimeSpan.Zero);
        var authentication = new SuccessfulAuthentication(expiresAt);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordAuthentication>();
                services.AddSingleton<IPasswordAuthentication>(authentication);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = "miriam@example.test",
                password = "Eine sichere Testpassphrase",
                rememberMe = true
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);
        Assert.Equal("access.jwt.value", body?.AccessToken);
        Assert.Equal(expiresAt, body?.ExpiresAt);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("freizeit_refresh=refresh.jwt.value", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("miriam@example.test", authentication.Request?.Email);
        Assert.True(authentication.Request?.RememberMe);
    }

    [Fact]
    public async Task FirstLoginCreatesInitialSuperAdminAndSignsIn()
    {
        var expiresAt = new DateTimeOffset(2026, 8, 10, 12, 15, 0, TimeSpan.Zero);
        var registration = new SuccessfulFirstLogin(expiresAt);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInitialSuperAdminRegistration>();
                services.AddSingleton<IInitialSuperAdminRegistration>(registration);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/first-login")
        {
            Content = JsonContent.Create(new
            {
                email = "erste-admin@example.test",
                password = "Eine sichere erste Passphrase",
                firstName = "Erika",
                lastName = "Admin"
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);
        Assert.Equal("first.access.jwt", body?.AccessToken);
        Assert.Equal("Erika", registration.Request?.FirstName);
        Assert.Equal("Admin", registration.Request?.LastName);
        Assert.Contains(
            "freizeit_refresh=first.refresh.jwt",
            Assert.Single(response.Headers.GetValues("Set-Cookie")),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshCookieIsRotatedAndReturnsNewAccessToken()
    {
        var expiresAt = new DateTimeOffset(2026, 8, 10, 12, 30, 0, TimeSpan.Zero);
        var sessions = new SuccessfulRefresh(expiresAt);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticationSessionManagement>();
                services.AddSingleton<IAuthenticationSessionManagement>(sessions);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);
        request.Headers.Add("Cookie", "freizeit_refresh=old.refresh.jwt");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);
        Assert.Equal("rotated.access.jwt", body?.AccessToken);
        Assert.Equal("old.refresh.jwt", sessions.Request?.RefreshToken);
        Assert.Contains(
            "freizeit_refresh=rotated.refresh.jwt",
            Assert.Single(response.Headers.GetValues("Set-Cookie")),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PasswordAuthenticationOutcome.InvalidCredentials, HttpStatusCode.Unauthorized)]
    [InlineData(PasswordAuthenticationOutcome.LockedOut, HttpStatusCode.Locked)]
    [InlineData(PasswordAuthenticationOutcome.RateLimited, HttpStatusCode.TooManyRequests)]
    public async Task LoginFailuresReturnStableProblem(
        PasswordAuthenticationOutcome outcome,
        HttpStatusCode expectedStatus)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordAuthentication>();
                services.AddSingleton<IPasswordAuthentication>(new FailedAuthentication(outcome));
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = "user@example.test",
                password = "Eine sichere Testpassphrase",
                rememberMe = false
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task FinishedFirstLoginAndMissingRefreshCookieAreRejected()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInitialSuperAdminRegistration>();
                services.AddSingleton<IInitialSuperAdminRegistration>(new UnavailableFirstLogin());
                services.RemoveAll<IAuthenticationSessionManagement>();
                services.AddSingleton<IAuthenticationSessionManagement>(new FailedSessions());
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var firstLogin = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/first-login")
        {
            Content = JsonContent.Create(new
            {
                email = "admin@example.test",
                password = "Eine sichere Admin-Passphrase",
                firstName = "Ada",
                lastName = "Lovelace"
            })
        };
        firstLogin.Headers.Add("X-CSRF-TOKEN", antiforgery);
        using var firstLoginResponse = await client.SendAsync(firstLogin, cancellationToken);
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.Add("X-CSRF-TOKEN", antiforgery);
        using var refreshResponse = await client.SendAsync(refresh, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, firstLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private static async Task<string> GetAntiforgeryAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
        return Assert.IsType<string>(payload?.Token);
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed class SuccessfulAuthentication(DateTimeOffset expiresAt) : IPasswordAuthentication
    {
        public PasswordLoginRequest? Request { get; private set; }

        public Task<PasswordAuthenticationResult> LoginAsync(
            PasswordLoginRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult(PasswordAuthenticationResult.Succeeded(
                new IssuedAuthentication(
                    Guid.Parse("30000000-0000-0000-0000-000000000001"),
                    new AccessTokenResponse("access.jwt.value", expiresAt),
                    "refresh.jwt.value",
                    expiresAt.AddDays(30),
                    true)));
        }
    }

    private sealed class FailedAuthentication(PasswordAuthenticationOutcome outcome)
        : IPasswordAuthentication
    {
        public Task<PasswordAuthenticationResult> LoginAsync(
            PasswordLoginRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(PasswordAuthenticationResult.Failed(outcome));
    }

    private sealed class SuccessfulFirstLogin(DateTimeOffset expiresAt) : IInitialSuperAdminRegistration
    {
        public InitialSuperAdminRequest? Request { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<InitialSuperAdminResult> RegisterAsync(
            InitialSuperAdminRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult(InitialSuperAdminResult.Succeeded(
                new IssuedAuthentication(
                    Guid.Parse("30000000-0000-0000-0000-000000000002"),
                    new AccessTokenResponse("first.access.jwt", expiresAt),
                    "first.refresh.jwt",
                    expiresAt.AddDays(30),
                    true)));
        }
    }

    private sealed class UnavailableFirstLogin : IInitialSuperAdminRegistration
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<InitialSuperAdminResult> RegisterAsync(
            InitialSuperAdminRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(InitialSuperAdminResult.Failed(
                InitialSuperAdminOutcome.AlreadyInitialized));
    }

    private sealed class SuccessfulRefresh(DateTimeOffset expiresAt) : IAuthenticationSessionManagement
    {
        public RefreshAuthenticationRequest? Request { get; private set; }

        public Task<RefreshAuthenticationResult> RefreshAsync(
            RefreshAuthenticationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult(RefreshAuthenticationResult.Succeeded(
                new IssuedAuthentication(
                    Guid.Parse("30000000-0000-0000-0000-000000000003"),
                    new AccessTokenResponse("rotated.access.jwt", expiresAt),
                    "rotated.refresh.jwt",
                    expiresAt.AddDays(30),
                    true)));
        }

        public Task<IReadOnlyList<SessionView>> ListSessionsAsync(
            Guid userId,
            Guid currentSessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SessionView>>([]);

        public Task RevokeSessionAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeOtherSessionsAsync(
            Guid userId,
            Guid currentSessionId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailedSessions : IAuthenticationSessionManagement
    {
        public Task<RefreshAuthenticationResult> RefreshAsync(
            RefreshAuthenticationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(RefreshAuthenticationResult.Failed(
                RefreshAuthenticationOutcome.Invalid));

        public Task<IReadOnlyList<SessionView>> ListSessionsAsync(
            Guid userId,
            Guid currentSessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SessionView>>([]);

        public Task RevokeSessionAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeOtherSessionsAsync(
            Guid userId,
            Guid currentSessionId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

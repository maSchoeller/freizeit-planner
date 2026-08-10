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

public sealed class TransferableInvitationApiTests
{
    private static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000021");

    [Fact]
    public async Task RegistrationAndConfirmationAreAntiforgeryProtectedAndStartSession()
    {
        var registration = new RegistrationFake();
        await using var factory = CreateFactory(registration);
        using var client = CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;

        using var missingAntiforgery = await client.PostAsJsonAsync(
            "/api/v1/invitations/invite-token/register",
            RegistrationBody(),
            cancellationToken);
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var begin = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/invitations/invite-token/register")
        {
            Content = JsonContent.Create(RegistrationBody())
        };
        begin.Headers.Add("X-CSRF-TOKEN", antiforgery);
        using var beginResponse = await client.SendAsync(begin, cancellationToken);
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var confirm = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations/confirm")
        {
            Content = JsonContent.Create(new { token = "confirmation-token" })
        };
        confirm.Headers.Add("X-CSRF-TOKEN", antiforgery);
        using var confirmResponse = await client.SendAsync(confirm, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingAntiforgery.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, beginResponse.StatusCode);
        Assert.Equal("invite-token", registration.BeginRequest?.InvitationToken);
        Assert.Equal("Erika", registration.BeginRequest?.FirstName);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Equal("confirmation-token", registration.Confirmation?.Token);
        Assert.Equal(
            "invitation.access.jwt",
            (await confirmResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken))?.AccessToken);
        Assert.Contains(
            "freizeit_refresh=invitation.refresh.jwt",
            Assert.Single(confirmResponse.Headers.GetValues("Set-Cookie")),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedInUserAcceptsTransferableInvitationForTheirGlobalAccount()
    {
        var registration = new RegistrationFake();
        await using var factory = CreateFactory(registration);
        using var client = CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Authorization = new("Test", UserId.ToString("D"));
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/invitations/invite-token/accept");
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        Assert.Equal(UserId, registration.Acceptance?.UserId);
        Assert.Equal("invite-token", registration.Acceptance?.InvitationToken);
    }

    [Fact]
    public async Task RotationAndRevocationRequireVersionAndAntiforgery()
    {
        var registration = new RegistrationFake();
        var links = new InvitationLinksFake();
        await using var factory = CreateFactory(registration, links);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Authorization = new("Test", UserId.ToString("D"));
        var cancellationToken = TestContext.Current.CancellationToken;
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var missingVersion = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/invitations/links/{links.InvitationId:D}/rotate");
        missingVersion.Headers.Add("X-CSRF-TOKEN", antiforgery);
        using var missingVersionResponse = await client.SendAsync(missingVersion, cancellationToken);
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var rotate = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/invitations/links/{links.InvitationId:D}/rotate");
        rotate.Headers.Add("X-CSRF-TOKEN", antiforgery);
        rotate.Headers.TryAddWithoutValidation("If-Match", "\"4\"");
        using var rotateResponse = await client.SendAsync(rotate, cancellationToken);
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var revoke = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/invitations/links/{links.InvitationId:D}");
        revoke.Headers.Add("X-CSRF-TOKEN", antiforgery);
        revoke.Headers.TryAddWithoutValidation("If-Match", "\"5\"");
        using var revokeResponse = await client.SendAsync(revoke, cancellationToken);

        Assert.Equal((HttpStatusCode)428, missingVersionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        Assert.Equal("\"1\"", rotateResponse.Headers.ETag?.Tag);
        Assert.Equal(4, links.RotateVersion);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        Assert.Equal(5, links.RevokeVersion);
    }

    private static object RegistrationBody() => new
    {
        email = "erika@example.test",
        password = "Eine sichere Einladungspassphrase",
        passwordConfirmation = "Eine sichere Einladungspassphrase",
        firstName = "Erika",
        lastName = "Muster"
    };

    private static WebApplicationFactory<Program> CreateFactory(
        IInvitationRegistration registration,
        ITransferableInvitationLinks? links = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInvitationRegistration>();
                services.AddSingleton(registration);
                if (links is not null)
                {
                    services.RemoveAll<ITransferableInvitationLinks>();
                    services.AddSingleton(links);
                }
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
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

    private sealed record AntiforgeryResponse(string Token);

    private sealed class RegistrationFake : IInvitationRegistration
    {
        public InvitationRegistrationRequest? BeginRequest { get; private set; }
        public InvitationEmailConfirmation? Confirmation { get; private set; }
        public ExistingInvitationAcceptance? Acceptance { get; private set; }

        public Task<InvitationRegistrationOutcome> BeginAsync(
            InvitationRegistrationRequest request,
            CancellationToken cancellationToken)
        {
            BeginRequest = request;
            return Task.FromResult(InvitationRegistrationOutcome.ConfirmationRequired);
        }

        public Task<InvitationConfirmationResult> ConfirmAsync(
            InvitationEmailConfirmation request,
            CancellationToken cancellationToken)
        {
            Confirmation = request;
            var expiresAt = new DateTimeOffset(2026, 8, 11, 12, 15, 0, TimeSpan.Zero);
            return Task.FromResult(InvitationConfirmationResult.Succeeded(
                InvitationGrant.SuperAdmin(),
                new IssuedAuthentication(
                    Guid.NewGuid(),
                    new AccessTokenResponse("invitation.access.jwt", expiresAt),
                    "invitation.refresh.jwt",
                    expiresAt.AddDays(30),
                    true)));
        }

        public Task<InvitationAcceptanceResult> AcceptExistingAsync(
            ExistingInvitationAcceptance request,
            CancellationToken cancellationToken)
        {
            Acceptance = request;
            return Task.FromResult(InvitationAcceptanceResult.Succeeded(InvitationGrant.SuperAdmin()));
        }
    }

    private sealed class InvitationLinksFake : ITransferableInvitationLinks
    {
        public Guid InvitationId { get; } = Guid.Parse("50000000-0000-0000-0000-000000000021");
        public long? RotateVersion { get; private set; }
        public long? RevokeVersion { get; private set; }

        public Task<IssuedInvitationLink> CreateAsync(
            CreateInvitationLinkRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<InvitationPreview?> PreviewAsync(
            string token,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IssuedInvitationLink> RotateAsync(
            Guid actorId,
            Guid invitationId,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            RotateVersion = expectedVersion;
            return Task.FromResult(new IssuedInvitationLink(
                Guid.NewGuid(),
                "replacement-token",
                InvitationGrant.SuperAdmin(),
                new DateTimeOffset(2026, 8, 11, 13, 0, 0, TimeSpan.Zero),
                1));
        }

        public Task RevokeAsync(
            Guid actorId,
            Guid invitationId,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            RevokeVersion = expectedVersion;
            return Task.CompletedTask;
        }
    }
}

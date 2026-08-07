using System.Net;
using System.Net.Http.Json;
using Identity.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public sealed class InvitationApiTests
{
    [Fact]
    public async Task AnonymousInviteAcceptanceIsAntiforgeryProtectedAndReturnsMembershipResult()
    {
        var lifecycle = new InvitationLifecycleFake();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IInvitationLifecycle>(lifecycle));
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;

        using var missingToken = await client.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new { token = "invitation-token", displayName = "Neue Person" },
            cancellationToken);
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations/accept")
        {
            Content = JsonContent.Create(new { token = "invitation-token", displayName = "Neue Person" })
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);
        using var response = await client.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<InvitationAcceptance>(cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(InvitationAcceptanceOutcome.Accepted, result?.Outcome);
        Assert.Equal("invitation-token", lifecycle.LastRequest?.Token);
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

    private sealed record AntiforgeryResponse(string Token);

    private sealed class InvitationLifecycleFake : IInvitationLifecycle
    {
        public AcceptInvitationRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<InvitationSummary>> ListInvitationsAsync(
            Guid actorId,
            Guid organizationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<InvitationAcceptance> AcceptInvitationAsync(
            AcceptInvitationRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new InvitationAcceptance(
                InvitationAcceptanceOutcome.Accepted,
                Guid.Parse("10000000-0000-0000-0000-000000000010"),
                Guid.Parse("20000000-0000-0000-0000-000000000010"),
                true));
        }

        public Task<IssuedInvitation> CreateOrganizationInvitationAsync(
            OrganizationInvitationRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IssuedInvitation> IssueTeamInvitationAsync(
            TeamInvitationRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IssuedInvitation> RotateInvitationAsync(
            Guid actorId,
            Guid invitationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RevokeInvitationAsync(
            Guid actorId,
            Guid invitationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

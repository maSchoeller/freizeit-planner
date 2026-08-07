using System.Net;
using System.Net.Http.Json;
using FreizeitCockpit.TestSupport;
using Identity.Contracts;
using Identity.Implementation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests;

public sealed class TenantAdministrationApiTests
{
    [Fact]
    public async Task MemberChangesRequireAntiforgeryAndIfMatch()
    {
        var administration = new AdministrationFake();
        var (client, sender) = CreateClient(administration);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);
            var csrf = await GetAntiforgeryAsync(client, cancellationToken);
            var uri = $"/api/v1/organizations/{administration.OrganizationId}/members/{administration.UserId}/role";

            using var noVersion = CreateJsonRequest(HttpMethod.Patch, uri, new { role = TenantRole.Member }, csrf);
            using var precondition = await client.SendAsync(noVersion, cancellationToken);
            csrf = await GetAntiforgeryAsync(client, cancellationToken);
            using var valid = CreateJsonRequest(HttpMethod.Patch, uri, new { role = TenantRole.Member }, csrf);
            valid.Headers.IfMatch.ParseAdd("\"3\"");
            using var changed = await client.SendAsync(valid, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var member = await changed.Content.ReadFromJsonAsync<OrganizationMemberView>(cancellationToken);

            Assert.Equal((HttpStatusCode)428, precondition.StatusCode);
            Assert.Equal("\"4\"", changed.Headers.ETag?.Tag);
            Assert.Equal(TenantRole.Member, member?.Role);
            Assert.Equal(3, administration.ExpectedVersion);
        }
    }

    [Fact]
    public async Task PlatformOrganizationListReturnsMetadataWithoutTenantContent()
    {
        var administration = new AdministrationFake();
        var (client, sender) = CreateClient(administration);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);

            using var response = await client.GetAsync("/api/v1/platform/organizations", cancellationToken);
            var organizations = await response.Content
                .ReadFromJsonAsync<PlatformOrganizationView[]>(cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var organization = Assert.Single(Assert.IsType<PlatformOrganizationView[]>(organizations));
            Assert.Equal("sonnenhoehe", organization.Slug);
            Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), administration.ActorId);
        }
    }

    private static (HttpClient Client, CapturingSender Sender) CreateClient(AdministrationFake administration)
    {
        var sender = new CapturingSender();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.RemoveAll<ITenantAdministration>();
                services.RemoveAll<IPlatformAdministration>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
                services.AddSingleton<ITenantAdministration>(administration);
                services.AddSingleton<IPlatformAdministration>(administration);
            });
        });
        return (factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        }), sender);
    }

    private static async Task LoginAsync(
        HttpClient client,
        CapturingSender sender,
        CancellationToken cancellationToken)
    {
        var csrf = await GetAntiforgeryAsync(client, cancellationToken);
        using var requestCode = CreateJsonRequest(
            HttpMethod.Post,
            "/api/v1/auth/code",
            new { email = "miriam@example.test" },
            csrf);
        using var requested = await client.SendAsync(requestCode, cancellationToken);
        requested.EnsureSuccessStatusCode();
        csrf = await GetAntiforgeryAsync(client, cancellationToken);
        using var verify = CreateJsonRequest(
            HttpMethod.Post,
            "/api/v1/auth/verify",
            new { email = "miriam@example.test", code = Assert.Single(sender.Codes), rememberMe = false },
            csrf);
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

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        string uri,
        object body,
        string csrf)
    {
        var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return request;
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed class CapturingSender : ILoginCodeSender
    {
        public List<string> Codes { get; } = [];

        public Task SendAsync(string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        {
            Codes.Add(code);
            return Task.CompletedTask;
        }
    }

    private sealed class AdministrationFake : ITenantAdministration, IPlatformAdministration
    {
        public Guid OrganizationId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");

        public Guid UserId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000004");

        public Guid ActorId { get; private set; }

        public long ExpectedVersion { get; private set; }

        public Task<IReadOnlyList<PlatformOrganizationView>> ListOrganizationsAsync(
            Guid actorId,
            CancellationToken cancellationToken)
        {
            ActorId = actorId;
            return Task.FromResult<IReadOnlyList<PlatformOrganizationView>>(
            [
                new(OrganizationId, "CVJM Sonnenhöhe", "sonnenhoehe", OrganizationStatus.Active, 1)
            ]);
        }

        public Task<OrganizationMemberView> ChangeOrganizationRoleAsync(
            OrganizationRoleChange change,
            CancellationToken cancellationToken)
        {
            ExpectedVersion = change.ExpectedVersion;
            return Task.FromResult(new OrganizationMemberView(
                change.UserId,
                change.Role,
                true,
                change.ExpectedVersion + 1,
                "member@example.test",
                "Teammitglied"));
        }

        public Task<IReadOnlyList<OrganizationMemberView>> ListOrganizationMembersAsync(
            Guid actorId,
            Guid organizationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveOrganizationMemberAsync(
            OrganizationMemberRemoval removal,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CampAssignmentView> AssignCampMemberAsync(
            CampMemberAssignment assignment,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveCampMemberAsync(
            CampMemberRemoval removal,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrganizationStatusView> ChangeOrganizationStatusAsync(
            OrganizationStatusChange change,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

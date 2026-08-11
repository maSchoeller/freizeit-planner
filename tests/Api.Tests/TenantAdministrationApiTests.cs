using System.Net;
using System.Net.Http.Json;
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
        var client = CreateClient(administration);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, cancellationToken);
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

    private static HttpClient CreateClient(AdministrationFake administration)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITenantAdministration>();
                services.AddSingleton<ITenantAdministration>(administration);
            });
        });
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    private static Task LoginAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        client.DefaultRequestHeaders.Authorization = new(
            "Test",
            "10000000-0000-0000-0000-000000000001");
        return Task.CompletedTask;
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

    private sealed class AdministrationFake : ITenantAdministration
    {
        public Guid OrganizationId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");

        public Guid UserId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000004");

        public long ExpectedVersion { get; private set; }

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

    }
}

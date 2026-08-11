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

public sealed class UserAdministrationApiTests
{
    private static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000031");
    private static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000032");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000031");

    [Fact]
    public async Task SuperAdminSearchIsPagedAndAuthenticated()
    {
        var administration = new AdministrationFake();
        await using var factory = CreateFactory(administration);
        using var client = CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;

        using var unauthorized = await client.GetAsync("/api/v1/superadmin/users", cancellationToken);
        client.DefaultRequestHeaders.Authorization = new("Test", ActorId.ToString("D"));
        using var response = await client.GetAsync(
            "/api/v1/superadmin/users?search=muster&page=2&pageSize=10",
            cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ActorId, administration.Query?.ActorId);
        Assert.Equal("muster", administration.Query?.Search);
        Assert.Equal(2, administration.Query?.Page);
        Assert.Equal(10, administration.Query?.PageSize);
    }

    [Fact]
    public async Task OrganizationMembershipMutationRequiresAntiforgeryAndVersion()
    {
        var administration = new AdministrationFake();
        await using var factory = CreateFactory(administration);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Authorization = new("Test", ActorId.ToString("D"));
        var cancellationToken = TestContext.Current.CancellationToken;
        var url = $"/api/v1/organizations/{OrganizationId:D}/administration/users/{UserId:D}/membership";

        using var withoutAntiforgery = await client.PutAsJsonAsync(url, MembershipBody(), cancellationToken);
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var withoutVersion = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(MembershipBody())
        };
        withoutVersion.Headers.Add("X-CSRF-TOKEN", antiforgery);
        using var withoutVersionResponse = await client.SendAsync(withoutVersion, cancellationToken);
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var valid = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(MembershipBody())
        };
        valid.Headers.Add("X-CSRF-TOKEN", antiforgery);
        valid.Headers.TryAddWithoutValidation("If-Match", "\"7\"");
        using var validResponse = await client.SendAsync(valid, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, withoutAntiforgery.StatusCode);
        Assert.Equal((HttpStatusCode)428, withoutVersionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.Equal("\"8\"", validResponse.Headers.ETag?.Tag);
        Assert.Equal(MembershipStatus.Suspended, administration.Membership?.Status);
        Assert.Equal(7, administration.Membership?.ExpectedVersion);
    }

    private static object MembershipBody() => new
    {
        status = MembershipStatus.Suspended,
        role = OrganizationRole.OrganizationAdmin
    };

    private static WebApplicationFactory<Program> CreateFactory(IUserAdministration administration) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserAdministration>();
                services.AddSingleton(administration);
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<string> GetAntiforgeryAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        return Assert.IsType<string>((await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(
            cancellationToken))?.Token);
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed class AdministrationFake : IUserAdministration
    {
        public UserAdministrationQuery? Query { get; private set; }

        public ChangeMembershipCommand? Membership { get; private set; }

        public Task<AdministrationPage<UserAdministrationView>> SearchUsersAsync(
            UserAdministrationQuery query,
            CancellationToken cancellationToken)
        {
            Query = query;
            return Task.FromResult(new AdministrationPage<UserAdministrationView>([], query.Page, query.PageSize, 0));
        }

        public Task<IReadOnlyList<SuperAdminOrganizationView>> ListOrganizationsAsync(
            Guid actorId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SuperAdminOrganizationView>>([]);

        public Task<UserAdministrationView> ChangeGlobalAccountStatusAsync(
            ChangeGlobalAccountStatusCommand command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<UserAdministrationView> ChangeSuperAdminAsync(
            ChangeSuperAdminCommand command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<UserAdministrationView> ClearLoginLockoutAsync(
            ClearLoginLockoutCommand command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrganizationAdministrationView> ChangeMembershipAsync(
            ChangeMembershipCommand command,
            CancellationToken cancellationToken)
        {
            Membership = command;
            return Task.FromResult(new OrganizationAdministrationView(
                command.OrganizationId,
                "Organization",
                "organization",
                command.Status,
                command.Role,
                [],
                command.ExpectedVersion + 1));
        }

        public Task<CampAdministrationView?> ChangeCampAssignmentAsync(
            ChangeCampAssignmentCommand command,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

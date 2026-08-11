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

public sealed class AccountLifecycleApiTests
{
    [Fact]
    public async Task ProfileRequiresIfMatchAndPersistsSeparateNames()
    {
        var lifecycle = new ProfileLifecycleFake();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountLifecycle>();
                services.AddSingleton<IAccountLifecycle>(lifecycle);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await LoginAsync(client, cancellationToken);
        var antiforgery = await GetAntiforgeryAsync(client, cancellationToken);

        using var missingVersion = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/account/profile")
        {
            Content = JsonContent.Create(new { firstName = "Miriam", lastName = "König" })
        };
        missingVersion.Headers.Add("X-CSRF-TOKEN", antiforgery);
        using var precondition = await client.SendAsync(missingVersion, cancellationToken);
        antiforgery = await GetAntiforgeryAsync(client, cancellationToken);
        using var valid = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/account/profile")
        {
            Content = JsonContent.Create(new { firstName = "Miriam", lastName = "König" })
        };
        valid.Headers.Add("X-CSRF-TOKEN", antiforgery);
        valid.Headers.IfMatch.ParseAdd("\"3\"");
        using var response = await client.SendAsync(valid, cancellationToken);
        var account = await response.Content.ReadFromJsonAsync<AccountView>(cancellationToken);

        Assert.Equal((HttpStatusCode)428, precondition.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Miriam", account?.FirstName);
        Assert.Equal("König", account?.LastName);
        Assert.Equal(3, lifecycle.ExpectedVersion);
        Assert.Equal("\"4\"", response.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task AuthenticatedUserCanVerifyANewEmailWithAntiforgeryProtection()
    {
        var lifecycle = new EmailChangeLifecycleFake();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailChangeLifecycle>();
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
        await LoginAsync(client, cancellationToken);

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

    private static HttpRequestMessage CreateJsonPost(string uri, object body, string antiforgery)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery);
        return request;
    }

    private sealed record AntiforgeryResponse(string Token);

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

    private sealed class ProfileLifecycleFake : IAccountLifecycle
    {
        public long ExpectedVersion { get; private set; }

        public Task<AccountView> UpdateProfileAsync(
            Guid userId,
            string firstName,
            string lastName,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            ExpectedVersion = expectedVersion;
            return Task.FromResult(new AccountView(
                userId,
                "miriam@example.test",
                $"{firstName} {lastName}",
                firstName,
                lastName,
                null,
                false,
                expectedVersion + 1));
        }

        public Task<AccountView> GetAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AccountMembershipView>> ListMembershipsAsync(
            Guid userId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DeletionSchedule> ScheduleAccountDeletionAsync(
            Guid userId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task CancelAccountDeletionAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LeaveOrganizationAsync(
            Guid userId,
            Guid organizationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DeletionSchedule> ScheduleOrganizationDeletionAsync(
            OrganizationDeletionRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task CancelOrganizationDeletionAsync(
            Guid actorId,
            Guid organizationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

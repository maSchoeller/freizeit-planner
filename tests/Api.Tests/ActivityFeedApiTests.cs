using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using Activity.Contracts;
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

public sealed class ActivityFeedApiTests
{
    [Fact]
    public async Task ActivityFeedIncludesTheCurrentActorDisplayNameAndObjectType()
    {
        var sender = new CapturingSender();
        var activity = new ActivityFake();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.RemoveAll<IActivityJournal>();
                services.RemoveAll<ICampMemberDirectory>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
                services.AddSingleton<IActivityJournal>(activity);
                services.AddSingleton<ICampMemberDirectory>(activity);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await LoginAsync(client, sender, cancellationToken);

        using var response = await client.GetAsync(
            $"/api/v1/organizations/{activity.OrganizationId}/camps/{activity.CampId}/activity?limit=5",
            cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var item = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Miriam Keller", item.GetProperty("actorDisplayName").GetString());
        Assert.Equal("ScheduleEntry", item.GetProperty("objectType").GetString());
        Assert.Equal("Ankommen", item.GetProperty("title").GetString());
    }

    private static async Task LoginAsync(
        HttpClient client,
        CapturingSender sender,
        CancellationToken cancellationToken)
    {
        var token = await GetAntiforgeryAsync(client, cancellationToken);
        using var requestCode = Post("/api/v1/auth/code", new { email = "miriam@example.test" }, token);
        using var requested = await client.SendAsync(requestCode, cancellationToken);
        requested.EnsureSuccessStatusCode();
        token = await GetAntiforgeryAsync(client, cancellationToken);
        using var verify = Post("/api/v1/auth/verify",
            new { email = "miriam@example.test", code = Assert.Single(sender.Codes), rememberMe = false }, token);
        using var verified = await client.SendAsync(verify, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
    }

    private static async Task<string> GetAntiforgeryAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
        return Assert.IsType<string>(body?.Token);
    }

    private static HttpRequestMessage Post(string path, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return request;
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed class CapturingSender : ILoginCodeSender
    {
        public List<string> Codes { get; } = [];

        public Task SendAsync(string email, string code, DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Codes.Add(code);
            return Task.CompletedTask;
        }
    }

    private sealed class ActivityFake : IActivityJournal, ICampMemberDirectory
    {
        public Guid OrganizationId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");

        public Guid CampId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000001");

        private Guid MiriamId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");

        public Task<IReadOnlyList<ActivityEvent>> ListAsync(
            ActivityQuery request,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ActivityEvent> result = [new ActivityEvent(
                Guid.Parse("40000000-0000-0000-0000-000000000001"),
                MiriamId,
                OrganizationId,
                CampId,
                ActivityKind.Created,
                "ScheduleEntry",
                Guid.Parse("50000000-0000-0000-0000-000000000001"),
                "Ankommen",
                DateTimeOffset.Parse("2026-08-09T10:00:00Z", CultureInfo.InvariantCulture),
                1)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<CampMemberSummary>> ListCampMembersAsync(
            CampMemberDirectoryQuery query,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<CampMemberSummary> result = [new CampMemberSummary(MiriamId, "Miriam Keller")];
            return Task.FromResult(result);
        }

        public Task<ActivityEvent> RecordAsync(
            RecordActivity request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

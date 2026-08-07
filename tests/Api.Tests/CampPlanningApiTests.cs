using System.Net;
using System.Net.Http.Json;
using Camps.Contracts;
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

public sealed class CampPlanningApiTests
{
    [Fact]
    public async Task CreatingCampRequiresAntiforgeryAndReturnsVersionEtag()
    {
        var planning = new PlanningFake();
        var (client, sender) = CreateClient(planning);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);
            var uri = $"/api/v1/organizations/{planning.OrganizationId}/camps";
            var body = new
            {
                name = "Sommerfreizeit",
                slug = "sommerfreizeit",
                description = "Gemeinsam unterwegs",
                startsOn = new DateOnly(2027, 7, 31),
                endsOn = new DateOnly(2027, 8, 8),
                timeZoneId = "Europe/Berlin",
                defaultPortions = 42
            };

            using var rejected = await client.PostAsJsonAsync(uri, body, cancellationToken);
            var csrf = await GetAntiforgeryAsync(client, cancellationToken);
            using var request = CreateJsonRequest(HttpMethod.Post, uri, body, csrf);
            using var response = await client.SendAsync(request, cancellationToken);
            var camp = await response.Content.ReadFromJsonAsync<CampView>(cancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
            Assert.Equal("sommerfreizeit", camp?.Slug);
            Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), planning.ActorId);
        }
    }

    [Fact]
    public async Task UpdatingCampRequiresIfMatch()
    {
        var planning = new PlanningFake();
        var (client, sender) = CreateClient(planning);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);
            var csrf = await GetAntiforgeryAsync(client, cancellationToken);
            var uri = $"/api/v1/organizations/{planning.OrganizationId}/camps/{planning.CampId}";
            var body = new
            {
                name = "Sommerfreizeit 2027",
                slug = "sommerfreizeit-2027",
                description = "Aktualisiert",
                startsOn = new DateOnly(2027, 7, 31),
                endsOn = new DateOnly(2027, 8, 8),
                timeZoneId = "Europe/Berlin",
                defaultPortions = 45
            };
            using var request = CreateJsonRequest(HttpMethod.Put, uri, body, csrf);
            using var missingVersion = await client.SendAsync(request, cancellationToken);

            csrf = await GetAntiforgeryAsync(client, cancellationToken);
            using var versioned = CreateJsonRequest(HttpMethod.Put, uri, body, csrf);
            versioned.Headers.IfMatch.ParseAdd("\"4\"");
            using var response = await client.SendAsync(versioned, cancellationToken);

            Assert.Equal((HttpStatusCode)428, missingVersion.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(4, planning.ExpectedVersion);
            Assert.Equal("\"5\"", response.Headers.ETag?.Tag);
        }
    }

    [Fact]
    public async Task ScheduleEndpointMapsLocalTimingAndVersion()
    {
        var planning = new PlanningFake();
        var (client, sender) = CreateClient(planning);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);
            var csrf = await GetAntiforgeryAsync(client, cancellationToken);
            var uri = $"/api/v1/organizations/{planning.OrganizationId}/camps/{planning.CampId}/schedule";
            var body = new
            {
                timing = new
                {
                    isAllDay = false,
                    localStart = new DateTime(2027, 8, 1, 9, 0, 0),
                    localEnd = new DateTime(2027, 8, 1, 10, 30, 0),
                    startDate = (DateOnly?)null,
                    endDateExclusive = (DateOnly?)null,
                    startChoice = AmbiguousLocalTimeChoice.Reject,
                    endChoice = AmbiguousLocalTimeChoice.Reject
                },
                title = "Morgenandacht",
                description = "Im Zelt",
                location = "Großes Zelt",
                category = "Andacht",
                status = ScheduleEntryStatus.Confirmed,
                responsibleUserIds = Array.Empty<Guid>(),
                audience = "Alle"
            };
            using var request = CreateJsonRequest(HttpMethod.Post, uri, body, csrf);
            using var response = await client.SendAsync(request, cancellationToken);
            var entry = await response.Content.ReadFromJsonAsync<ScheduleEntryView>(cancellationToken);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
            Assert.Equal(new DateTime(2027, 8, 1, 9, 0, 0), planning.LocalStart);
            Assert.Equal("Morgenandacht", entry?.Title);
        }
    }

    private static (HttpClient Client, CapturingSender Sender) CreateClient(PlanningFake planning)
    {
        var sender = new CapturingSender();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.RemoveAll<ICampManagement>();
                services.RemoveAll<ISchedulePlanning>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
                services.AddSingleton<ICampManagement>(planning);
                services.AddSingleton<ISchedulePlanning>(planning);
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

        public Task SendAsync(
            string email,
            string code,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Codes.Add(code);
            return Task.CompletedTask;
        }
    }

    private sealed class PlanningFake : ICampManagement, ISchedulePlanning
    {
        public Guid OrganizationId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");

        public Guid CampId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000001");

        public Guid ActorId { get; private set; }

        public long ExpectedVersion { get; private set; }

        public DateTime? LocalStart { get; private set; }

        public Task<CampView> CreateAsync(CreateCamp command, CancellationToken cancellationToken)
        {
            ActorId = command.ActorId;
            return Task.FromResult(Camp(command.Name, command.Slug, 1));
        }

        public Task<CampView> UpdateAsync(UpdateCamp command, CancellationToken cancellationToken)
        {
            ExpectedVersion = command.ExpectedVersion;
            return Task.FromResult(Camp(command.Name, command.Slug, command.ExpectedVersion + 1));
        }

        public Task<ScheduleEntryView> CreateAsync(
            CreateScheduleEntry command,
            CancellationToken cancellationToken)
        {
            LocalStart = command.Timing.LocalStart;
            return Task.FromResult(new ScheduleEntryView(
                Guid.Parse("40000000-0000-0000-0000-000000000001"),
                OrganizationId,
                CampId,
                new ScheduleTimingView(
                    false,
                    new DateTimeOffset(2027, 8, 1, 7, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2027, 8, 1, 8, 30, 0, TimeSpan.Zero),
                    null,
                    null,
                    "Europe/Berlin"),
                command.Title,
                command.Description,
                command.Location,
                command.Category,
                command.Status,
                command.ResponsibleUserIds,
                command.Audience,
                false,
                1));
        }

        private CampView Camp(string name, string slug, long version) => new(
            CampId,
            OrganizationId,
            name,
            slug,
            "Gemeinsam unterwegs",
            new DateOnly(2027, 7, 31),
            new DateOnly(2027, 8, 8),
            "Europe/Berlin",
            42,
            CampStatus.Active,
            CampPeriod.Upcoming,
            version);

        public Task<IReadOnlyList<CampSummary>> ListAsync(
            CampListQuery query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CampView> GetBySlugAsync(
            CampBySlugQuery query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CampView> ChangeStatusAsync(
            ChangeCampStatus command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ScheduleEntryView>> ListAsync(
            ScheduleRangeQuery query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ScheduleEntryView> GetAsync(
            ScheduleEntryQuery query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ScheduleEntryView> UpdateAsync(
            UpdateScheduleEntry command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ScheduleEntryReference> DeleteAsync(
            DeleteScheduleEntry command,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

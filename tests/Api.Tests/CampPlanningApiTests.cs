using System.Net;
using System.Net.Http.Json;
using Activity.Contracts;
using Camps.Contracts;
using Catering.Contracts;
using FreizeitCockpit.TestSupport;
using Identity.Contracts;
using Identity.Implementation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spiritual.Contracts;
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
            var activity = Assert.Single(planning.Activities);
            Assert.Equal(ActivityKind.Created, activity.Kind);
            Assert.Equal("ScheduleEntry", activity.ObjectType);
            Assert.Equal("Morgenandacht", activity.Title);
            var searchDocument = Assert.Single(planning.SearchDocuments);
            Assert.Equal("ScheduleEntry", searchDocument.ObjectType);
            Assert.Equal("Andacht", searchDocument.Metadata["category"]);
        }
    }

    [Fact]
    public async Task ScheduleAndMealCanBeCreatedAsOneLinkedWorkflow()
    {
        var planning = new PlanningFake();
        var (client, sender) = CreateClient(planning);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);
            var csrf = await GetAntiforgeryAsync(client, cancellationToken);
            var uri = $"/api/v1/organizations/{planning.OrganizationId}/camps/{planning.CampId}/schedule/with-meal";
            var body = new
            {
                schedule = new
                {
                    timing = new
                    {
                        isAllDay = false,
                        localStart = new DateTime(2027, 8, 1, 12, 0, 0),
                        localEnd = new DateTime(2027, 8, 1, 13, 0, 0),
                        startDate = (DateOnly?)null,
                        endDateExclusive = (DateOnly?)null,
                        startChoice = AmbiguousLocalTimeChoice.Reject,
                        endChoice = AmbiguousLocalTimeChoice.Reject
                    },
                    title = "Mittagessen",
                    description = "Gemeinsames Essen",
                    location = "Speisesaal",
                    category = "Essen",
                    status = ScheduleEntryStatus.Confirmed,
                    responsibleUserIds = Array.Empty<Guid>(),
                    audience = "Alle"
                },
                meal = new
                {
                    name = "Kartoffelsuppe",
                    portionOverride = (int?)null,
                    recipeIds = Array.Empty<Guid>()
                }
            };
            using var request = CreateJsonRequest(HttpMethod.Post, uri, body, csrf);

            using var response = await client.SendAsync(request, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<LinkedScheduleMealResponse>(cancellationToken);
            Assert.Equal(planning.ScheduleEntryId, result?.ScheduleEntry.Id);
            Assert.Equal(planning.ScheduleEntryId, result?.Meal.ScheduleEntryId);
            Assert.Equal("Kartoffelsuppe", result?.Meal.Name);
        }
    }

    [Fact]
    public async Task ScheduleAndDevotionCanBeCreatedAsOneLinkedWorkflow()
    {
        var planning = new PlanningFake();
        var (client, sender) = CreateClient(planning);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);
            var csrf = await GetAntiforgeryAsync(client, cancellationToken);
            var uri = $"/api/v1/organizations/{planning.OrganizationId}/camps/{planning.CampId}/schedule/with-devotion";
            var body = new
            {
                schedule = new
                {
                    timing = new
                    {
                        isAllDay = false,
                        localStart = new DateTime(2027, 8, 1, 20, 0, 0),
                        localEnd = new DateTime(2027, 8, 1, 20, 30, 0),
                        startDate = (DateOnly?)null,
                        endDateExclusive = (DateOnly?)null,
                        startChoice = AmbiguousLocalTimeChoice.Reject,
                        endChoice = AmbiguousLocalTimeChoice.Reject
                    },
                    title = "Abendandacht",
                    description = "Gemeinsamer Abschluss",
                    location = "Kapelle",
                    category = "Andacht",
                    status = ScheduleEntryStatus.Confirmed,
                    responsibleUserIds = Array.Empty<Guid>(),
                    audience = "Alle"
                },
                devotion = new
                {
                    topic = "Vertrauen",
                    bibleReference = "Psalm 23",
                    translation = BibleTranslation.Luther1912,
                    coreMessage = "Gott begleitet uns.",
                    markdownContent = "# Vertrauen",
                    responsibleUserIds = Array.Empty<Guid>(),
                    materialNotes = "Kerze"
                }
            };
            using var request = CreateJsonRequest(HttpMethod.Post, uri, body, csrf);

            using var response = await client.SendAsync(request, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<LinkedScheduleDevotionResponse>(cancellationToken);
            Assert.Equal(planning.ScheduleEntryId, result?.ScheduleEntry.Id);
            Assert.Equal(planning.ScheduleEntryId, result?.Devotion.ScheduleEntryId);
            Assert.Equal("Vertrauen", result?.Devotion.Topic);
            Assert.Equal(
                [Guid.Parse("10000000-0000-0000-0000-000000000001")],
                result?.Devotion.ResponsibleUserIds);
        }
    }

    [Fact]
    public async Task ActivityFailureRollsBackThePlanningResponse()
    {
        var planning = new PlanningFake { FailActivity = true };
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
                    isAllDay = true,
                    localStart = (DateTime?)null,
                    localEnd = (DateTime?)null,
                    startDate = new DateOnly(2027, 8, 1),
                    endDateExclusive = new DateOnly(2027, 8, 2),
                    startChoice = AmbiguousLocalTimeChoice.Reject,
                    endChoice = AmbiguousLocalTimeChoice.Reject
                },
                title = "Ausflug",
                description = "",
                location = "",
                category = "Programm",
                status = ScheduleEntryStatus.Planned,
                responsibleUserIds = Array.Empty<Guid>(),
                audience = "Alle"
            };
            using var request = CreateJsonRequest(HttpMethod.Post, uri, body, csrf);

            using var response = await client.SendAsync(request, cancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task LinkedScheduleDeletionRequiresChoiceAndCanMoveEveryLinkToTrash()
    {
        var planning = new PlanningFake();
        var (client, sender) = CreateClient(planning);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);
            var uri = $"/api/v1/organizations/{planning.OrganizationId}/camps/{planning.CampId}/schedule/{planning.ScheduleEntryId}";

            var csrf = await GetAntiforgeryAsync(client, cancellationToken);
            using var missingChoice = CreateDeleteRequest(uri, csrf, 1);
            using var rejected = await client.SendAsync(missingChoice, cancellationToken);

            csrf = await GetAntiforgeryAsync(client, cancellationToken);
            using var commonTrash = CreateDeleteRequest(
                $"{uri}?linkedBehavior=MoveLinkedToTrash", csrf, 1);
            using var accepted = await client.SendAsync(commonTrash, cancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            Assert.True(planning.MealMovedToTrash);
            Assert.True(planning.DevotionMovedToTrash);
            Assert.True(planning.ScheduleMovedToTrash);
        }
    }

    [Fact]
    public async Task LinkedScheduleDeletionCanExplicitlyUnlinkEveryLink()
    {
        var planning = new PlanningFake();
        var (client, sender) = CreateClient(planning);
        using (client)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await LoginAsync(client, sender, cancellationToken);
            var csrf = await GetAntiforgeryAsync(client, cancellationToken);
            var uri = $"/api/v1/organizations/{planning.OrganizationId}/camps/{planning.CampId}/schedule/{planning.ScheduleEntryId}?linkedBehavior=Unlink";
            using var request = CreateDeleteRequest(uri, csrf, 1);

            using var response = await client.SendAsync(request, cancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(planning.MealUnlinked);
            Assert.True(planning.DevotionUnlinked);
            Assert.True(planning.ScheduleMovedToTrash);
            Assert.False(planning.MealMovedToTrash);
            Assert.False(planning.DevotionMovedToTrash);
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
                services.RemoveAll<IActivityJournal>();
                services.RemoveAll<ICampSearchIndex>();
                services.RemoveAll<ICampMealPlanning>();
                services.RemoveAll<IDevotionPlanning>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
                services.AddSingleton<ICampManagement>(planning);
                services.AddSingleton<ISchedulePlanning>(planning);
                services.AddSingleton<IActivityJournal>(planning);
                services.AddSingleton<ICampSearchIndex>(planning);
                services.AddSingleton<ICampMealPlanning>(planning);
                services.AddSingleton<IDevotionPlanning>(planning);
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

    private static HttpRequestMessage CreateDeleteRequest(string uri, string csrf, long version)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.IfMatch.ParseAdd($"\"{version}\"");
        return request;
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed record LinkedScheduleMealResponse(ScheduleEntryView ScheduleEntry, Meal Meal);

    private sealed record LinkedScheduleDevotionResponse(
        ScheduleEntryView ScheduleEntry,
        DevotionDetails Devotion);

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

    private sealed class PlanningFake : ICampManagement, ISchedulePlanning, IActivityJournal, ICampSearchIndex,
        ICampMealPlanning, IDevotionPlanning
    {
        public Guid OrganizationId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");

        public Guid CampId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000001");

        public Guid ScheduleEntryId { get; } = Guid.Parse("40000000-0000-0000-0000-000000000001");

        public bool MealMovedToTrash { get; private set; }

        public bool DevotionMovedToTrash { get; private set; }

        public bool ScheduleMovedToTrash { get; private set; }

        public bool MealUnlinked { get; private set; }

        public bool DevotionUnlinked { get; private set; }

        public Guid ActorId { get; private set; }

        public long ExpectedVersion { get; private set; }

        public DateTime? LocalStart { get; private set; }

        public List<RecordActivity> Activities { get; } = [];

        public List<UpsertSearchDocument> SearchDocuments { get; } = [];

        public bool FailActivity { get; init; }

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

        private ScheduleEntryView ScheduleEntry(string title, long version) => new(
            ScheduleEntryId,
            OrganizationId,
            CampId,
            new ScheduleTimingView(
                true,
                null,
                null,
                new DateOnly(2027, 8, 1),
                new DateOnly(2027, 8, 2),
                "Europe/Berlin"),
            title,
            null,
            null,
            "Programm",
            ScheduleEntryStatus.Planned,
            [],
            null,
            false,
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
            CancellationToken cancellationToken) => Task.FromResult(ScheduleEntry("Zeitplaneintrag", 1));

        public Task<ScheduleEntryView> UpdateAsync(
            UpdateScheduleEntry command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ScheduleEntryReference> DeleteAsync(
            DeleteScheduleEntry command,
            CancellationToken cancellationToken)
        {
            ScheduleMovedToTrash = true;
            return Task.FromResult(new ScheduleEntryReference(OrganizationId, CampId, ScheduleEntryId,
                command.ExpectedVersion + 1));
        }

        public Task<IReadOnlyList<TrashedScheduleEntry>> ListTrashAsync(
            ScheduleTrashQuery query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ScheduleEntryView> RestoreAsync(
            RestoreScheduleEntry command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<MealSummary>> ListMealsAsync(
            CampCateringQuery request,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MealSummary>>([
                new MealSummary(Guid.Parse("50000000-0000-0000-0000-000000000001"), OrganizationId, CampId,
                    "Mittagessen", 20, ScheduleEntryId, 1, 3)
            ]);

        public Task<Meal?> GetMealAsync(MealRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<Meal?>(new Meal(
                request.MealId,
                OrganizationId,
                CampId,
                "Mittagessen",
                20,
                null,
                20,
                ScheduleEntryId,
                [],
                3));

        public Task MoveMealToTrashAsync(DeleteMeal request, CancellationToken cancellationToken)
        {
            MealMovedToTrash = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrashedMeal>> ListMealTrashAsync(
            MealTrashQuery request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<DevotionSummary>> ListAsync(
            DevotionScope scope,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DevotionSummary>>([
                new DevotionSummary(Guid.Parse("60000000-0000-0000-0000-000000000001"), OrganizationId, CampId,
                    "Abendandacht", "Johannes 3,16", BibleTranslation.Schlachter1951, [], ScheduleEntryId, false, 4)
            ]);

        public Task MoveToTrashAsync(
            ChangeDevotionLifecycle command,
            CancellationToken cancellationToken)
        {
            DevotionMovedToTrash = true;
            return Task.CompletedTask;
        }

        public Task<Meal> CreateMealAsync(CreateMeal request, CancellationToken cancellationToken) =>
            Task.FromResult(new Meal(
                Guid.Parse("50000000-0000-0000-0000-000000000001"),
                request.OrganizationId,
                request.CampId,
                request.Name,
                20,
                request.PortionOverride,
                request.PortionOverride ?? 20,
                request.ScheduleEntryId,
                [],
                1));
        public Task<Meal> ReviseMealAsync(ReviseMeal request, CancellationToken cancellationToken)
        {
            MealUnlinked = request.ScheduleEntryId is null;
            return Task.FromResult(new Meal(
                request.MealId,
                OrganizationId,
                CampId,
                request.Name,
                20,
                request.PortionOverride,
                request.PortionOverride ?? 20,
                request.ScheduleEntryId,
                [],
                request.ExpectedVersion + 1));
        }
        public Task<Meal> RestoreMealAsync(RestoreMeal request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Meal> AddRecipeSnapshotAsync(AddRecipeSnapshot request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Meal> RemoveRecipeSnapshotAsync(RemoveRecipeSnapshot request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Meal> RefreshRecipeSnapshotAsync(RefreshRecipeSnapshot request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TrashedDevotion>> ListTrashAsync(DevotionScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DevotionDetails?> GetAsync(DevotionKey key, CancellationToken cancellationToken) =>
            Task.FromResult<DevotionDetails?>(new DevotionDetails(
                key.DevotionId,
                OrganizationId,
                CampId,
                "Abendandacht",
                "Johannes 3,16",
                BibleTranslation.Schlachter1951,
                "Gott liebt die Welt.",
                "# Abendandacht",
                [],
                "Kerze",
                ScheduleEntryId,
                null,
                new DateTimeOffset(2027, 8, 1, 16, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 8, 1, 16, 0, 0, TimeSpan.Zero),
                null,
                4));
        public Task<DevotionDetails> CreateAsync(CreateDevotion command, CancellationToken cancellationToken) =>
            Task.FromResult(new DevotionDetails(
                Guid.Parse("60000000-0000-0000-0000-000000000001"),
                command.OrganizationId,
                command.CampId,
                command.Topic,
                command.BibleReference,
                command.Translation,
                command.CoreMessage,
                command.MarkdownContent,
                command.ResponsibleUserIds,
                command.MaterialNotes,
                command.ScheduleEntryId,
                null,
                new DateTimeOffset(2027, 8, 1, 18, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 8, 1, 18, 0, 0, TimeSpan.Zero),
                null,
                1));
        public Task<DevotionDetails> UpdateAsync(UpdateDevotion command, CancellationToken cancellationToken)
        {
            DevotionUnlinked = command.ScheduleEntryId is null;
            return Task.FromResult(new DevotionDetails(
                command.DevotionId,
                OrganizationId,
                CampId,
                command.Topic,
                command.BibleReference,
                command.Translation,
                command.CoreMessage,
                command.MarkdownContent,
                command.ResponsibleUserIds,
                command.MaterialNotes,
                command.ScheduleEntryId,
                null,
                new DateTimeOffset(2027, 8, 1, 16, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 8, 1, 17, 0, 0, TimeSpan.Zero),
                null,
                command.ExpectedVersion + 1));
        }
        public Task RestoreAsync(ChangeDevotionLifecycle command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BibleSnapshotRefreshResult> RefreshBibleSnapshotAsync(RefreshBibleSnapshot command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DevotionDetails> SaveManualBibleSnapshotAsync(SaveManualBibleSnapshot command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<BibleTranslationView>> ListBibleTranslationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ActivityEvent> RecordAsync(RecordActivity request, CancellationToken cancellationToken)
        {
            if (FailActivity) throw new ActivityRuleException("activity_unavailable", "Aktivität nicht verfügbar.");
            Activities.Add(request);
            return Task.FromResult(new ActivityEvent(Guid.NewGuid(), request.ActorId, request.OrganizationId,
                request.CampId, request.Kind, request.ObjectType, request.ObjectId, request.Title,
                request.Timestamp, Activities.Count));
        }

        public Task<IReadOnlyList<ActivityEvent>> ListAsync(
            ActivityQuery request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SearchProjectionResult> UpsertAsync(
            UpsertSearchDocument request,
            CancellationToken cancellationToken)
        {
            SearchDocuments.Add(request);
            return Task.FromResult(new SearchProjectionResult(request.ObjectType, request.ObjectId,
                request.SourceVersion, SearchDocuments.Count, true, false));
        }

        public Task<SearchProjectionResult> RemoveAsync(
            RemoveSearchDocument request,
            CancellationToken cancellationToken) => Task.FromResult(new SearchProjectionResult(
                request.ObjectType, request.ObjectId, request.SourceVersion, SearchDocuments.Count + 1,
                false, true));

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            CampSearchQuery request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

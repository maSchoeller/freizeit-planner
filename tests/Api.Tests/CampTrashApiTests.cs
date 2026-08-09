using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using Camps.Contracts;
using Catering.Contracts;
using Files.Contracts;
using FreizeitCockpit.TestSupport;
using Identity.Contracts;
using Identity.Implementation;
using Knowledge.Contracts;
using Logistics.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spiritual.Contracts;
using Xunit;

namespace Api.Tests;

public sealed class CampTrashApiTests
{
    [Fact]
    public async Task CampManagerReceivesOneChronologicalTrashAcrossModules()
    {
        var sender = new CapturingSender();
        var trash = new TrashFake();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.RemoveAll<ICampNotebook>();
                services.RemoveAll<IDevotionPlanning>();
                services.RemoveAll<IAttachmentCatalog>();
                services.RemoveAll<IMaterialPlanning>();
                services.RemoveAll<IShoppingPlanning>();
                services.RemoveAll<ISchedulePlanning>();
                services.RemoveAll<ICampMealPlanning>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
                services.AddSingleton<ICampNotebook>(trash);
                services.AddSingleton<IDevotionPlanning>(trash);
                services.AddSingleton<IAttachmentCatalog>(trash);
                services.AddSingleton<IMaterialPlanning>(trash);
                services.AddSingleton<IShoppingPlanning>(trash);
                services.AddSingleton<ISchedulePlanning>(trash);
                services.AddSingleton<ICampMealPlanning>(trash);
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
            $"/api/v1/organizations/{trash.OrganizationId}/camps/{trash.CampId}/trash",
            cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var items = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        Assert.Equal(["Datei.pdf", "Andacht", "Notiz", "Material", "Einkauf", "Brot", "Tagesplan", "Mahlzeit"],
            items.Select(item => item.GetProperty("title").GetString()));
        Assert.All(items, item => Assert.EndsWith(
            "/restore",
            item.GetProperty("restorePath").GetString(),
            StringComparison.Ordinal));
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), trash.ActorId);
    }

    [Theory]
    [InlineData("Camps")]
    [InlineData("Catering")]
    public async Task CampTrashReturnsAForbiddenProblemForModuleAccessDenial(string failingModule)
    {
        var sender = new CapturingSender();
        var trash = new TrashFake(failingModule);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.RemoveAll<ICampNotebook>();
                services.RemoveAll<IDevotionPlanning>();
                services.RemoveAll<IAttachmentCatalog>();
                services.RemoveAll<IMaterialPlanning>();
                services.RemoveAll<IShoppingPlanning>();
                services.RemoveAll<ISchedulePlanning>();
                services.RemoveAll<ICampMealPlanning>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
                services.AddSingleton<ICampNotebook>(trash);
                services.AddSingleton<IDevotionPlanning>(trash);
                services.AddSingleton<IAttachmentCatalog>(trash);
                services.AddSingleton<IMaterialPlanning>(trash);
                services.AddSingleton<IShoppingPlanning>(trash);
                services.AddSingleton<ISchedulePlanning>(trash);
                services.AddSingleton<ICampMealPlanning>(trash);
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
            $"/api/v1/organizations/{trash.OrganizationId}/camps/{trash.CampId}/trash",
            cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("camp_access_denied", document.RootElement.GetProperty("errorCode").GetString());
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

    private sealed class TrashFake(string? failingModule = null) : ICampNotebook, IDevotionPlanning, IAttachmentCatalog, IMaterialPlanning,
        IShoppingPlanning, ISchedulePlanning, ICampMealPlanning
    {
        public Guid OrganizationId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");

        public Guid CampId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000001");

        public Guid ActorId { get; private set; }

        public Task<IReadOnlyList<NoteSummary>> ListNotesAsync(
            NotebookQuery request,
            CancellationToken cancellationToken)
        {
            ActorId = request.ActorId;
            IReadOnlyList<NoteSummary> result = [new NoteSummary(
                Guid.Parse("40000000-0000-0000-0000-000000000001"), OrganizationId, CampId, "Notiz", "", [],
                false, 0, NoteState.Trashed, ParseTimestamp("2026-08-07T10:00:00Z"),
                ParseTimestamp("2026-08-07T10:00:00Z"), ParseTimestamp("2026-09-06T10:00:00Z"), 2)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<TrashedDevotion>> ListTrashAsync(
            DevotionScope scope,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<TrashedDevotion> result = [new TrashedDevotion(
                Guid.Parse("40000000-0000-0000-0000-000000000002"), OrganizationId, CampId, "Andacht",
                ParseTimestamp("2026-08-08T10:00:00Z"), ParseTimestamp("2026-09-07T10:00:00Z"), 3)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<AttachmentView>> ListTrashAsync(
            AttachmentTrashQuery query,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttachmentView> result = [new AttachmentView(
                Guid.Parse("40000000-0000-0000-0000-000000000003"), OrganizationId, CampId,
                new AttachmentOwnerReference(AttachmentOwnerType.Note, Guid.NewGuid()), "Datei.pdf",
                AttachmentMediaType.Pdf, "application/pdf", 42, AttachmentLifecycleState.Deleted, query.ActorId,
                ParseTimestamp("2026-08-01T10:00:00Z"), ParseTimestamp("2026-08-09T10:00:00Z"),
                ParseTimestamp("2026-09-08T10:00:00Z"), 4)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<TrashedMaterialRequirement>> ListTrashAsync(
            MaterialTrashQuery query,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<TrashedMaterialRequirement> result = [new TrashedMaterialRequirement(
                Guid.Parse("40000000-0000-0000-0000-000000000004"), OrganizationId, CampId, "Material",
                ParseTimestamp("2026-08-06T10:00:00Z"), ParseTimestamp("2026-09-05T10:00:00Z"), 5)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<TrashedShoppingList>> ListTrashAsync(
            ShoppingTrashQuery query,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<TrashedShoppingList> result = [new TrashedShoppingList(
                Guid.Parse("40000000-0000-0000-0000-000000000005"), OrganizationId, CampId, "Einkauf",
                ParseTimestamp("2026-08-05T10:00:00Z"), ParseTimestamp("2026-09-04T10:00:00Z"), 6)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<TrashedShoppingItem>> ListItemTrashAsync(
            ShoppingItemTrashQuery query,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<TrashedShoppingItem> result = [new TrashedShoppingItem(
                Guid.Parse("40000000-0000-0000-0000-000000000006"),
                Guid.Parse("40000000-0000-0000-0000-000000000005"),
                OrganizationId,
                CampId,
                "Brot",
                ParseTimestamp("2026-08-04T10:00:00Z"),
                ParseTimestamp("2026-09-03T10:00:00Z"),
                7)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<TrashedScheduleEntry>> ListTrashAsync(
            ScheduleTrashQuery query,
            CancellationToken cancellationToken)
        {
            if (failingModule == "Camps")
                throw new CampsRuleException("camp_access_denied", "Kein Zugriff auf den Camp-Papierkorb.");
            IReadOnlyList<TrashedScheduleEntry> result = [new TrashedScheduleEntry(
                Guid.Parse("40000000-0000-0000-0000-000000000007"), OrganizationId, CampId, "Tagesplan",
                ParseTimestamp("2026-08-03T10:00:00Z"), ParseTimestamp("2026-09-02T10:00:00Z"), 8)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<TrashedMeal>> ListMealTrashAsync(
            MealTrashQuery request,
            CancellationToken cancellationToken)
        {
            if (failingModule == "Catering")
                throw new CateringRuleException("camp_access_denied", "Kein Zugriff auf den Camp-Papierkorb.");
            IReadOnlyList<TrashedMeal> result = [new TrashedMeal(
                Guid.Parse("40000000-0000-0000-0000-000000000008"), OrganizationId, CampId, "Mahlzeit", null,
                ParseTimestamp("2026-08-02T10:00:00Z"), ParseTimestamp("2026-09-01T10:00:00Z"), 9)];
            return Task.FromResult(result);
        }

        public Task<Note?> GetNoteAsync(NoteRequest request, CancellationToken cancellationToken) => Unsupported<Note?>();
        public Task<Note> CreateNoteAsync(CreateNote request, CancellationToken cancellationToken) => Unsupported<Note>();
        public Task<Note> ReviseNoteAsync(ReviseNote request, CancellationToken cancellationToken) => Unsupported<Note>();
        public Task<Note> MoveNoteToTrashAsync(MoveNoteToTrash request, CancellationToken cancellationToken) => Unsupported<Note>();
        public Task<Note> RestoreNoteAsync(RestoreNote request, CancellationToken cancellationToken) => Unsupported<Note>();
        public Task<IReadOnlyList<DevotionSummary>> ListAsync(DevotionScope scope, CancellationToken cancellationToken) => Unsupported<IReadOnlyList<DevotionSummary>>();
        public Task<DevotionDetails?> GetAsync(DevotionKey key, CancellationToken cancellationToken) => Unsupported<DevotionDetails?>();
        public Task<DevotionDetails> CreateAsync(CreateDevotion command, CancellationToken cancellationToken) => Unsupported<DevotionDetails>();
        public Task<DevotionDetails> UpdateAsync(UpdateDevotion command, CancellationToken cancellationToken) => Unsupported<DevotionDetails>();
        public Task MoveToTrashAsync(ChangeDevotionLifecycle command, CancellationToken cancellationToken) => Unsupported();
        public Task RestoreAsync(ChangeDevotionLifecycle command, CancellationToken cancellationToken) => Unsupported();
        public Task<BibleSnapshotRefreshResult> RefreshBibleSnapshotAsync(RefreshBibleSnapshot command, CancellationToken cancellationToken) => Unsupported<BibleSnapshotRefreshResult>();
        public Task<DevotionDetails> SaveManualBibleSnapshotAsync(SaveManualBibleSnapshot command, CancellationToken cancellationToken) => Unsupported<DevotionDetails>();
        public Task<IReadOnlyList<BibleTranslationView>> ListBibleTranslationsAsync(CancellationToken cancellationToken) => Unsupported<IReadOnlyList<BibleTranslationView>>();
        public Task<IReadOnlyList<AttachmentView>> ListAsync(AttachmentOwnerQuery query, CancellationToken cancellationToken) => Unsupported<IReadOnlyList<AttachmentView>>();
        public Task<AttachmentView> UploadAsync(UploadAttachment command, Stream content, CancellationToken cancellationToken) => Unsupported<AttachmentView>();
        public Task<AttachmentView> MoveToTrashAsync(ChangeAttachmentLifecycle command, CancellationToken cancellationToken) => Unsupported<AttachmentView>();
        public Task<AttachmentView> RestoreAsync(ChangeAttachmentLifecycle command, CancellationToken cancellationToken) => Unsupported<AttachmentView>();
        public Task<AttachmentQuotaView> GetQuotaAsync(AttachmentQuotaQuery query, CancellationToken cancellationToken) => Unsupported<AttachmentQuotaView>();
        public Task<IReadOnlyList<MaterialRequirementSummary>> ListAsync(MaterialQuery query, CancellationToken cancellationToken) => Unsupported<IReadOnlyList<MaterialRequirementSummary>>();
        public Task<MaterialRequirement?> GetAsync(MaterialRequest request, CancellationToken cancellationToken) => Unsupported<MaterialRequirement?>();
        public Task<MaterialRequirement> CreateAsync(CreateMaterialRequirement command, CancellationToken cancellationToken) => Unsupported<MaterialRequirement>();
        public Task<MaterialRequirement> UpdateAsync(UpdateMaterialRequirement command, CancellationToken cancellationToken) => Unsupported<MaterialRequirement>();
        public Task DeleteAsync(DeleteMaterialRequirement command, CancellationToken cancellationToken) => Unsupported();
        public Task<MaterialRequirement> RestoreAsync(RestoreMaterialRequirement command, CancellationToken cancellationToken) => Unsupported<MaterialRequirement>();
        public Task<IReadOnlyList<ShoppingListSummary>> ListAsync(ShoppingListsQuery query, CancellationToken cancellationToken) => Unsupported<IReadOnlyList<ShoppingListSummary>>();
        public Task<ShoppingList?> GetAsync(ShoppingListRequest request, CancellationToken cancellationToken) => Unsupported<ShoppingList?>();
        public Task<ShoppingList> CreateListAsync(CreateShoppingList command, CancellationToken cancellationToken) => Unsupported<ShoppingList>();
        public Task<ShoppingList> RenameListAsync(RenameShoppingList command, CancellationToken cancellationToken) => Unsupported<ShoppingList>();
        public Task DeleteListAsync(DeleteShoppingList command, CancellationToken cancellationToken) => Unsupported();
        public Task<ShoppingList> RestoreListAsync(RestoreShoppingList command, CancellationToken cancellationToken) => Unsupported<ShoppingList>();
        public Task<ShoppingListChange> AddSpontaneousItemAsync(AddSpontaneousShoppingItem command, CancellationToken cancellationToken) => Unsupported<ShoppingListChange>();
        public Task<ShoppingListChange> UpdateItemAsync(UpdateShoppingItem command, CancellationToken cancellationToken) => Unsupported<ShoppingListChange>();
        public Task<ShoppingListChange> SetItemCheckedAsync(SetShoppingItemChecked command, CancellationToken cancellationToken) => Unsupported<ShoppingListChange>();
        public Task<ShoppingListChange> DeleteItemAsync(DeleteShoppingItem command, CancellationToken cancellationToken) => Unsupported<ShoppingListChange>();
        public Task<ShoppingListChange> RestoreItemAsync(RestoreShoppingItem command, CancellationToken cancellationToken) => Unsupported<ShoppingListChange>();
        public Task<IReadOnlyList<ScheduleEntryView>> ListAsync(ScheduleRangeQuery query, CancellationToken cancellationToken) => Unsupported<IReadOnlyList<ScheduleEntryView>>();
        public Task<ScheduleEntryView> GetAsync(ScheduleEntryQuery query, CancellationToken cancellationToken) => Unsupported<ScheduleEntryView>();
        public Task<ScheduleEntryView> CreateAsync(CreateScheduleEntry command, CancellationToken cancellationToken) => Unsupported<ScheduleEntryView>();
        public Task<ScheduleEntryView> UpdateAsync(UpdateScheduleEntry command, CancellationToken cancellationToken) => Unsupported<ScheduleEntryView>();
        public Task<ScheduleEntryReference> DeleteAsync(DeleteScheduleEntry command, CancellationToken cancellationToken) => Unsupported<ScheduleEntryReference>();
        public Task<ScheduleEntryView> RestoreAsync(RestoreScheduleEntry command, CancellationToken cancellationToken) => Unsupported<ScheduleEntryView>();
        public Task<IReadOnlyList<MealSummary>> ListMealsAsync(CampCateringQuery request, CancellationToken cancellationToken) => Unsupported<IReadOnlyList<MealSummary>>();
        public Task<Meal?> GetMealAsync(MealRequest request, CancellationToken cancellationToken) => Unsupported<Meal?>();
        public Task<Meal> CreateMealAsync(CreateMeal request, CancellationToken cancellationToken) => Unsupported<Meal>();
        public Task<Meal> ReviseMealAsync(ReviseMeal request, CancellationToken cancellationToken) => Unsupported<Meal>();
        public Task MoveMealToTrashAsync(DeleteMeal request, CancellationToken cancellationToken) => Unsupported();
        public Task<Meal> RestoreMealAsync(RestoreMeal request, CancellationToken cancellationToken) => Unsupported<Meal>();
        public Task<Meal> AddRecipeSnapshotAsync(AddRecipeSnapshot request, CancellationToken cancellationToken) => Unsupported<Meal>();
        public Task<Meal> RemoveRecipeSnapshotAsync(RemoveRecipeSnapshot request, CancellationToken cancellationToken) => Unsupported<Meal>();
        public Task<Meal> RefreshRecipeSnapshotAsync(RefreshRecipeSnapshot request, CancellationToken cancellationToken) => Unsupported<Meal>();

        private static Task Unsupported() => throw new NotSupportedException();

        private static Task<T> Unsupported<T>() => throw new NotSupportedException();

        private static DateTimeOffset ParseTimestamp(string value) =>
            DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }
}

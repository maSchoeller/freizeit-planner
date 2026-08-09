using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using Files.Contracts;
using FreizeitCockpit.TestSupport;
using Identity.Contracts;
using Identity.Implementation;
using Knowledge.Contracts;
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
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
                services.AddSingleton<ICampNotebook>(trash);
                services.AddSingleton<IDevotionPlanning>(trash);
                services.AddSingleton<IAttachmentCatalog>(trash);
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
        Assert.Equal(["Datei.pdf", "Andacht", "Notiz"],
            items.Select(item => item.GetProperty("title").GetString()));
        Assert.All(items, item => Assert.EndsWith(
            "/restore",
            item.GetProperty("restorePath").GetString(),
            StringComparison.Ordinal));
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), trash.ActorId);
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

    private sealed class TrashFake : ICampNotebook, IDevotionPlanning, IAttachmentCatalog
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
        public Task MoveToTrashAsync(ChangeAttachmentLifecycle command, CancellationToken cancellationToken) => Unsupported();
        public Task<AttachmentView> RestoreAsync(ChangeAttachmentLifecycle command, CancellationToken cancellationToken) => Unsupported<AttachmentView>();
        public Task<AttachmentQuotaView> GetQuotaAsync(AttachmentQuotaQuery query, CancellationToken cancellationToken) => Unsupported<AttachmentQuotaView>();

        private static Task Unsupported() => throw new NotSupportedException();

        private static Task<T> Unsupported<T>() => throw new NotSupportedException();

        private static DateTimeOffset ParseTimestamp(string value) =>
            DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }
}

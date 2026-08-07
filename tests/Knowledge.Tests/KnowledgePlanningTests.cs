using Identity.Contracts;
using Knowledge.Contracts;
using Knowledge.Implementation;
using Xunit;

namespace Knowledge.Tests;

public sealed class KnowledgePlanningTests
{
    private static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CampId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task CreateNormalizesSharedFieldsAndProducesSanitizedHtml()
    {
        var context = new TestKnowledgeCampContext();
        var subject = CreateSubject(context: context);

        var note = await subject.CreateNoteAsync(
            new CreateNote(
                ActorId,
                OrganizationId,
                CampId,
                Content(
                    "  Erste\tSchritte  ",
                    "## Plan\n\n**Wichtig** und *jetzt*\n\n- Bibeln\n- [Webseite](https://example.test/path?q=1&ok=2)",
                    ["  Leitung ", "LEITUNG", " Ablauf "])),
            TestContext.Current.CancellationToken);

        Assert.Equal(ActorId, context.LastRequest!.ActorId);
        Assert.Equal("Erste Schritte", note.Title);
        Assert.Equal(["Ablauf", "Leitung"], note.Tags);
        Assert.Contains("<h2>Plan</h2>", note.RenderedHtml, StringComparison.Ordinal);
        Assert.Contains("<strong>Wichtig</strong>", note.RenderedHtml, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", note.RenderedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript", note.RenderedHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "raw_html_not_allowed")]
    [InlineData("Titel | Wert\n--- | ---\nA | B", "markdown_table_not_allowed")]
    [InlineData("![Bild](https://example.test/a.png)", "markdown_image_not_allowed")]
    [InlineData("[Klick](javascript:alert(1))", "markdown_link_not_allowed")]
    public async Task CreateRejectsMarkdownOutsideTheSafeSubset(string markdown, string errorCode)
    {
        var subject = CreateSubject();

        var exception = await Assert.ThrowsAsync<KnowledgeRuleException>(() => subject.CreateNoteAsync(
            new CreateNote(ActorId, OrganizationId, CampId, Content("Notiz", markdown)),
            TestContext.Current.CancellationToken));

        Assert.Equal(errorCode, exception.ErrorCode);
    }

    [Fact]
    public async Task RevisionUsesExpectedVersionAndPreservesNoBlindMerge()
    {
        var subject = CreateSubject();
        var note = await CreateNoteAsync(subject);

        var revised = await subject.ReviseNoteAsync(
            new ReviseNote(
                ActorId,
                OrganizationId,
                CampId,
                note.Id,
                note.Version,
                Content("Geändert", "Neuer Inhalt", ["Neu"])),
            TestContext.Current.CancellationToken);
        var conflict = await Assert.ThrowsAsync<KnowledgeRuleException>(() => subject.ReviseNoteAsync(
            new ReviseNote(
                ActorId,
                OrganizationId,
                CampId,
                note.Id,
                note.Version,
                Content("Veraltet", "Veralteter Inhalt")),
            TestContext.Current.CancellationToken));

        Assert.Equal(2, revised.Version);
        Assert.Equal("Geändert", revised.Title);
        Assert.Equal("concurrency_conflict", conflict.ErrorCode);
    }

    [Fact]
    public async Task TypedLinksAreDeduplicatedValidatedAndSnapshotted()
    {
        var scheduleEntryId = Guid.NewGuid();
        var resolver = new TestNoteLinkResolver
        {
            Titles = { [new NoteLinkReference(NoteLinkTargetType.ScheduleEntry, scheduleEntryId)] = "Morgenandacht" }
        };
        var subject = CreateSubject(resolver: resolver);
        var reference = new NoteLinkReference(NoteLinkTargetType.ScheduleEntry, scheduleEntryId);

        var note = await subject.CreateNoteAsync(
            new CreateNote(
                ActorId,
                OrganizationId,
                CampId,
                Content("Ablauf", "Siehe Planung", links: [reference, reference])),
            TestContext.Current.CancellationToken);

        var link = Assert.Single(note.Links);
        Assert.Equal("Morgenandacht", link.TargetTitle);
        Assert.Single(resolver.LastRequest!.Links);

        resolver.ReturnIncompleteResult = true;
        var invalid = await Assert.ThrowsAsync<KnowledgeRuleException>(() => subject.ReviseNoteAsync(
            new ReviseNote(
                ActorId,
                OrganizationId,
                CampId,
                note.Id,
                note.Version,
                Content("Ablauf", "Siehe Planung", links: [reference])),
            TestContext.Current.CancellationToken));
        Assert.Equal("invalid_note_link", invalid.ErrorCode);
    }

    [Fact]
    public async Task TrashIsHiddenFromActiveListAndLeadershipCanRestoreIt()
    {
        var subject = CreateSubject();
        var note = await CreateNoteAsync(subject);
        var trashed = await subject.MoveNoteToTrashAsync(
            new MoveNoteToTrash(ActorId, OrganizationId, CampId, note.Id, note.Version),
            TestContext.Current.CancellationToken);

        var active = await subject.ListNotesAsync(
            new NotebookQuery(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken);
        var trash = await subject.ListNotesAsync(
            new NotebookQuery(ActorId, OrganizationId, CampId, NotebookSection.Trash),
            TestContext.Current.CancellationToken);
        var restored = await subject.RestoreNoteAsync(
            new RestoreNote(ActorId, OrganizationId, CampId, note.Id, trashed.Version),
            TestContext.Current.CancellationToken);

        Assert.Empty(active);
        Assert.Single(trash);
        Assert.Equal(NoteState.Active, restored.State);
        Assert.Null(restored.TrashedAt);
        Assert.Equal(3, restored.Version);
    }

    [Fact]
    public async Task MemberCannotBrowseOrRestoreTrash()
    {
        var state = new TestKnowledgeState();
        var leader = CreateSubject(state: state);
        var note = await CreateNoteAsync(leader);
        var trashed = await leader.MoveNoteToTrashAsync(
            new MoveNoteToTrash(ActorId, OrganizationId, CampId, note.Id, note.Version),
            TestContext.Current.CancellationToken);
        var member = CreateSubject(state: state, access: new MemberAccessControl());

        var browse = await Assert.ThrowsAsync<KnowledgeRuleException>(() => member.ListNotesAsync(
            new NotebookQuery(ActorId, OrganizationId, CampId, NotebookSection.Trash),
            TestContext.Current.CancellationToken));
        var restore = await Assert.ThrowsAsync<KnowledgeRuleException>(() => member.RestoreNoteAsync(
            new RestoreNote(ActorId, OrganizationId, CampId, note.Id, trashed.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("access_denied", browse.ErrorCode);
        Assert.Equal("access_denied", restore.ErrorCode);
    }

    [Fact]
    public async Task ArchivedCampRejectsEveryNotebookMutation()
    {
        var context = new TestKnowledgeCampContext();
        var subject = CreateSubject(context: context);
        var note = await CreateNoteAsync(subject);
        var trashed = await subject.MoveNoteToTrashAsync(
            new MoveNoteToTrash(ActorId, OrganizationId, CampId, note.Id, note.Version),
            TestContext.Current.CancellationToken);
        context.IsArchived = true;

        var create = () => subject.CreateNoteAsync(
            new CreateNote(ActorId, OrganizationId, CampId, Content("Neu", "Text")),
            TestContext.Current.CancellationToken);
        var revise = () => subject.ReviseNoteAsync(
            new ReviseNote(ActorId, OrganizationId, CampId, note.Id, trashed.Version, Content("Neu", "Text")),
            TestContext.Current.CancellationToken);
        var remove = () => subject.MoveNoteToTrashAsync(
            new MoveNoteToTrash(ActorId, OrganizationId, CampId, note.Id, trashed.Version),
            TestContext.Current.CancellationToken);
        var restore = () => subject.RestoreNoteAsync(
            new RestoreNote(ActorId, OrganizationId, CampId, note.Id, trashed.Version),
            TestContext.Current.CancellationToken);

        foreach (var mutation in new Func<Task>[] { create, revise, remove, restore })
        {
            var exception = await Assert.ThrowsAsync<KnowledgeRuleException>(mutation);
            Assert.Equal("camp_archived", exception.ErrorCode);
        }
    }

    [Fact]
    public async Task RetentionPurgesOnlyNotesWhoseThirtyDaysHaveElapsed()
    {
        var now = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var state = new TestKnowledgeState();
        var subject = CreateSubject(state: state, timeProvider: clock);
        var first = await CreateNoteAsync(subject, "Erste");
        var second = await CreateNoteAsync(subject, "Zweite");
        await subject.MoveNoteToTrashAsync(
            new MoveNoteToTrash(ActorId, OrganizationId, CampId, first.Id, first.Version),
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromDays(1));
        await subject.MoveNoteToTrashAsync(
            new MoveNoteToTrash(ActorId, OrganizationId, CampId, second.Id, second.Version),
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromDays(29));

        var result = await subject.PurgeExpiredNotesAsync(100, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.PurgedNotes);
        Assert.DoesNotContain(state.Notes, item => item.Id == first.Id);
        Assert.Contains(state.Notes, item => item.Id == second.Id);
    }

    private static KnowledgeService CreateSubject(
        TestKnowledgeState? state = null,
        ITenantAccessControl? access = null,
        TestKnowledgeCampContext? context = null,
        TestNoteLinkResolver? resolver = null,
        TimeProvider? timeProvider = null) =>
        new(
            state ?? new TestKnowledgeState(),
            access ?? new AllowKnowledgeAccessControl(),
            context ?? new TestKnowledgeCampContext(),
            resolver ?? new TestNoteLinkResolver(),
            timeProvider ?? TimeProvider.System);

    private static Task<Note> CreateNoteAsync(KnowledgeService subject, string title = "Notiz") =>
        subject.CreateNoteAsync(
            new CreateNote(ActorId, OrganizationId, CampId, Content(title, "Inhalt", ["Team"])),
            TestContext.Current.CancellationToken);

    private static NoteContent Content(
        string title,
        string markdown,
        IReadOnlyList<string>? tags = null,
        bool isPinned = false,
        IReadOnlyList<NoteLinkReference>? links = null) =>
        new(title, markdown, tags ?? [], isPinned, links ?? []);
}

internal sealed class TestKnowledgeCampContext : IKnowledgeCampContext
{
    public bool IsArchived { get; set; }

    public KnowledgeCampContextRequest? LastRequest { get; private set; }

    public Task<KnowledgeCampContext> GetAsync(
        KnowledgeCampContextRequest request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new KnowledgeCampContext(IsArchived));
    }
}

internal sealed class TestNoteLinkResolver : INoteLinkTargetResolver
{
    public Dictionary<NoteLinkReference, string> Titles { get; } = [];

    public bool ReturnIncompleteResult { get; set; }

    public NoteLinkResolutionRequest? LastRequest { get; private set; }

    public Task<IReadOnlyList<ResolvedNoteLink>> ResolveAsync(
        NoteLinkResolutionRequest request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (ReturnIncompleteResult)
        {
            return Task.FromResult<IReadOnlyList<ResolvedNoteLink>>([]);
        }

        return Task.FromResult<IReadOnlyList<ResolvedNoteLink>>(request.Links.Select(link =>
            new ResolvedNoteLink(
                link.Type,
                link.TargetId,
                Titles.GetValueOrDefault(link, $"{link.Type} {link.TargetId:N}"))).ToList());
    }
}

internal sealed class AllowKnowledgeAccessControl : ITenantAccessControl
{
    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Permit(TenantRole.Owner));

    public Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Permit(TenantRole.Owner));
}

internal sealed class MemberAccessControl : ITenantAccessControl
{
    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Permit(TenantRole.Member));

    public Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(request.Action == CampAction.ManageCamp
            ? TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied)
            : TenantAccessDecision.Permit(TenantRole.Member));
}

internal sealed class TestKnowledgeState : IKnowledgeState
{
    public List<NoteEntity> Notes { get; } = [];

    public Task<IReadOnlyList<NoteEntity>> ListNotesAsync(
        Guid organizationId,
        Guid campId,
        NoteState state,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<NoteEntity>>(Notes.Where(item =>
            item.OrganizationId == organizationId && item.CampId == campId && item.State == state).ToList());

    public Task<NoteEntity?> FindNoteAsync(
        Guid organizationId,
        Guid campId,
        Guid noteId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Notes.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.CampId == campId && item.Id == noteId));

    public Task<IReadOnlyList<NoteEntity>> FindExpiredNotesAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<NoteEntity>>(Notes
            .Where(item => item.State == NoteState.Trashed && item.PurgeAfter <= now)
            .OrderBy(item => item.PurgeAfter)
            .Take(limit)
            .ToList());

    public void AddNote(NoteEntity note) => Notes.Add(note);

    public void RemoveNotes(IEnumerable<NoteEntity> notes)
    {
        foreach (var note in notes.ToList())
        {
            Notes.Remove(note);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset now = now;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan duration) => now += duration;
}

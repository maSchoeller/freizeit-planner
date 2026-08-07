using System.Text;
using Identity.Contracts;
using Knowledge.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Knowledge.Implementation;

public sealed class KnowledgeService : ICampNotebook, INotebookRetention
{
    private readonly IKnowledgeState state;
    private readonly ITenantAccessControl accessControl;
    private readonly IKnowledgeCampContext campContext;
    private readonly INoteLinkTargetResolver linkResolver;
    private readonly TimeProvider timeProvider;

    public KnowledgeService(
        KnowledgeDbContext dbContext,
        ITenantAccessControl accessControl,
        IKnowledgeCampContext campContext,
        INoteLinkTargetResolver linkResolver,
        TimeProvider timeProvider)
        : this(new EfKnowledgeState(dbContext), accessControl, campContext, linkResolver, timeProvider)
    {
    }

    internal KnowledgeService(
        IKnowledgeState state,
        ITenantAccessControl accessControl,
        IKnowledgeCampContext campContext,
        INoteLinkTargetResolver linkResolver,
        TimeProvider timeProvider)
    {
        this.state = state;
        this.accessControl = accessControl;
        this.campContext = campContext;
        this.linkResolver = linkResolver;
        this.timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<NoteSummary>> ListNotesAsync(
        NotebookQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            request.Section == NotebookSection.Trash ? CampAction.ManageCamp : CampAction.Read,
            cancellationToken);
        await GetCampContextAsync(request.ActorId, request.OrganizationId, request.CampId, cancellationToken);

        var desiredState = request.Section == NotebookSection.Trash ? NoteState.Trashed : NoteState.Active;
        var notes = await state.ListNotesAsync(
            request.OrganizationId,
            request.CampId,
            desiredState,
            cancellationToken);
        var normalizedTag = string.IsNullOrWhiteSpace(request.Tag) ? null : NormalizeKey(request.Tag);
        var normalizedSearch = string.IsNullOrWhiteSpace(request.SearchText) ? null : NormalizeKey(request.SearchText);
        var filtered = notes.Where(note =>
            (normalizedTag is null || note.Tags.Any(tag => tag.NormalizedName == normalizedTag)) &&
            (normalizedSearch is null ||
             NormalizeKey(note.Title).Contains(normalizedSearch, StringComparison.Ordinal) ||
             NormalizeKey(note.Markdown).Contains(normalizedSearch, StringComparison.Ordinal)));

        return request.Section == NotebookSection.Trash
            ? filtered.OrderByDescending(item => item.TrashedAt).Select(MapSummary).ToList()
            : filtered.OrderByDescending(item => item.IsPinned)
                .ThenByDescending(item => item.UpdatedAt)
                .Select(MapSummary)
                .ToList();
    }

    public async Task<Note?> GetNoteAsync(NoteRequest request, CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.Read,
            cancellationToken);
        await GetCampContextAsync(request.ActorId, request.OrganizationId, request.CampId, cancellationToken);
        var note = await state.FindNoteAsync(
            request.OrganizationId,
            request.CampId,
            request.NoteId,
            cancellationToken);
        if (note?.State == NoteState.Trashed)
        {
            await EnsureCampAccessAsync(
                request.ActorId,
                request.OrganizationId,
                request.CampId,
                CampAction.ManageCamp,
                cancellationToken);
        }

        return note is null ? null : MapNote(note);
    }

    public async Task<Note> CreateNoteAsync(CreateNote request, CancellationToken cancellationToken)
    {
        await EnsureCampWriteAsync(request.ActorId, request.OrganizationId, request.CampId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var note = new NoteEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            CampId = request.CampId,
            Title = string.Empty,
            Markdown = string.Empty,
            CreatedAt = now,
            CreatedBy = request.ActorId,
            UpdatedAt = now,
            UpdatedBy = request.ActorId
        };
        await ApplyContentAsync(note, request.ActorId, request.Content, cancellationToken);
        state.AddNote(note);
        await SaveAsync(cancellationToken);
        return MapNote(note);
    }

    public async Task<Note> ReviseNoteAsync(ReviseNote request, CancellationToken cancellationToken)
    {
        await EnsureCampWriteAsync(request.ActorId, request.OrganizationId, request.CampId, cancellationToken);
        var note = await RequireNoteAsync(
            request.OrganizationId,
            request.CampId,
            request.NoteId,
            cancellationToken);
        EnsureActive(note);
        EnsureVersion(note.Version, request.ExpectedVersion);
        await ApplyContentAsync(note, request.ActorId, request.Content, cancellationToken);
        note.UpdatedAt = timeProvider.GetUtcNow();
        note.UpdatedBy = request.ActorId;
        note.Version++;
        await SaveAsync(cancellationToken);
        return MapNote(note);
    }

    public async Task<Note> MoveNoteToTrashAsync(
        MoveNoteToTrash request,
        CancellationToken cancellationToken)
    {
        await EnsureCampWriteAsync(request.ActorId, request.OrganizationId, request.CampId, cancellationToken);
        var note = await RequireNoteAsync(
            request.OrganizationId,
            request.CampId,
            request.NoteId,
            cancellationToken);
        EnsureActive(note);
        EnsureVersion(note.Version, request.ExpectedVersion);
        var now = timeProvider.GetUtcNow();
        note.State = NoteState.Trashed;
        note.TrashedAt = now;
        note.TrashedBy = request.ActorId;
        note.PurgeAfter = now.AddDays(30);
        note.UpdatedAt = now;
        note.UpdatedBy = request.ActorId;
        note.Version++;
        await SaveAsync(cancellationToken);
        return MapNote(note);
    }

    public async Task<Note> RestoreNoteAsync(RestoreNote request, CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.ManageCamp,
            cancellationToken);
        await EnsureCampNotArchivedAsync(request.ActorId, request.OrganizationId, request.CampId, cancellationToken);
        var note = await RequireNoteAsync(
            request.OrganizationId,
            request.CampId,
            request.NoteId,
            cancellationToken);
        if (note.State != NoteState.Trashed)
        {
            throw Rule("note_not_trashed", "Die Notiz befindet sich nicht im Papierkorb.");
        }

        EnsureVersion(note.Version, request.ExpectedVersion);
        var now = timeProvider.GetUtcNow();
        note.State = NoteState.Active;
        note.TrashedAt = null;
        note.TrashedBy = null;
        note.PurgeAfter = null;
        note.UpdatedAt = now;
        note.UpdatedBy = request.ActorId;
        note.Version++;
        await SaveAsync(cancellationToken);
        return MapNote(note);
    }

    public async Task<NotePurgeResult> PurgeExpiredNotesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var notes = await state.FindExpiredNotesAsync(
            timeProvider.GetUtcNow(),
            Math.Clamp(batchSize, 1, 500),
            cancellationToken);
        state.RemoveNotes(notes);
        await SaveAsync(cancellationToken);
        return new NotePurgeResult(notes.Count);
    }

    private async Task ApplyContentAsync(
        NoteEntity note,
        Guid actorId,
        NoteContent content,
        CancellationToken cancellationToken)
    {
        var title = NormalizeDisplayName(
            content.Title,
            160,
            "note_title_required",
            "Bitte einen Titel für die Notiz angeben.");
        _ = SafeMarkdownProcessor.Process(content.Markdown);
        var tags = NormalizeTags(content.Tags);
        var links = await ResolveLinksAsync(
            actorId,
            note.OrganizationId,
            note.CampId,
            content.Links,
            cancellationToken);

        note.Title = title;
        note.Markdown = content.Markdown;
        note.IsPinned = content.IsPinned;
        note.Tags.Clear();
        note.Tags.AddRange(tags.Select(tag => new NoteTagEntity
        {
            Id = Guid.NewGuid(),
            NoteId = note.Id,
            OrganizationId = note.OrganizationId,
            CampId = note.CampId,
            DisplayName = tag,
            NormalizedName = NormalizeKey(tag)
        }));
        note.Links.Clear();
        note.Links.AddRange(links.Select(link => new NoteLinkEntity
        {
            Id = Guid.NewGuid(),
            NoteId = note.Id,
            OrganizationId = note.OrganizationId,
            CampId = note.CampId,
            TargetType = link.Type,
            TargetId = link.TargetId,
            TargetTitleSnapshot = link.TargetTitle
        }));
    }

    private async Task<IReadOnlyList<ResolvedNoteLink>> ResolveLinksAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        IReadOnlyList<NoteLinkReference> links,
        CancellationToken cancellationToken)
    {
        var distinct = links.Distinct().ToList();
        if (distinct.Count > 20)
        {
            throw Rule("too_many_note_links", "Eine Notiz darf höchstens 20 Verknüpfungen enthalten.");
        }

        if (distinct.Count == 0)
        {
            return [];
        }

        var resolved = await linkResolver.ResolveAsync(
            new NoteLinkResolutionRequest(actorId, organizationId, campId, distinct),
            cancellationToken);
        if (resolved.Count != distinct.Count ||
            resolved.Select(item => new NoteLinkReference(item.Type, item.TargetId)).Distinct().Count() != distinct.Count)
        {
            throw InvalidLink();
        }

        var byReference = resolved.ToDictionary(item => new NoteLinkReference(item.Type, item.TargetId));
        var result = new List<ResolvedNoteLink>(distinct.Count);
        foreach (var reference in distinct)
        {
            if (!byReference.TryGetValue(reference, out var item))
            {
                throw InvalidLink();
            }

            string targetTitle;
            try
            {
                targetTitle = NormalizeDisplayName(
                    item.TargetTitle,
                    160,
                    "invalid_note_link",
                    "Mindestens eine Verknüpfung ist ungültig oder nicht zugänglich.");
            }
            catch (KnowledgeRuleException)
            {
                throw InvalidLink();
            }

            result.Add(item with { TargetTitle = targetTitle });
        }

        return result;
    }

    private static List<string> NormalizeTags(IReadOnlyList<string> tags)
    {
        if (tags.Count > 12)
        {
            throw Rule("too_many_note_tags", "Eine Notiz darf höchstens zwölf Tags enthalten.");
        }

        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var value in tags)
        {
            var display = NormalizeDisplayName(
                value,
                40,
                "invalid_note_tag",
                "Tags dürfen nicht leer sein.");
            byKey.TryAdd(NormalizeKey(display), display);
        }

        return byKey.Values.OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static NoteSummary MapSummary(NoteEntity note)
    {
        var markdown = SafeMarkdownProcessor.Process(note.Markdown);
        var excerpt = CollapseWhitespace(markdown.PlainText);
        if (excerpt.Length > 240)
        {
            excerpt = $"{excerpt[..237]}...";
        }

        return new NoteSummary(
            note.Id,
            note.OrganizationId,
            note.CampId,
            note.Title,
            excerpt,
            note.Tags.Select(item => item.DisplayName).OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase).ToList(),
            note.IsPinned,
            note.Links.Count,
            note.State,
            note.UpdatedAt,
            note.Version);
    }

    private static Note MapNote(NoteEntity note)
    {
        var markdown = SafeMarkdownProcessor.Process(note.Markdown);
        return new Note(
            note.Id,
            note.OrganizationId,
            note.CampId,
            note.Title,
            note.Markdown,
            markdown.RenderedHtml,
            note.Tags.Select(item => item.DisplayName).OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase).ToList(),
            note.IsPinned,
            note.Links.Select(item => new NoteLink(item.TargetType, item.TargetId, item.TargetTitleSnapshot)).ToList(),
            note.State,
            note.CreatedAt,
            note.CreatedBy,
            note.UpdatedAt,
            note.UpdatedBy,
            note.TrashedAt,
            note.TrashedBy,
            note.PurgeAfter,
            note.Version);
    }

    private async Task<NoteEntity> RequireNoteAsync(
        Guid organizationId,
        Guid campId,
        Guid noteId,
        CancellationToken cancellationToken) =>
        await state.FindNoteAsync(organizationId, campId, noteId, cancellationToken) ??
            throw Rule("note_not_found", "Die Notiz wurde nicht gefunden.");

    private async Task EnsureCampWriteAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(actorId, organizationId, campId, CampAction.WriteContent, cancellationToken);
        await EnsureCampNotArchivedAsync(actorId, organizationId, campId, cancellationToken);
    }

    private async Task EnsureCampNotArchivedAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var context = await GetCampContextAsync(actorId, organizationId, campId, cancellationToken);
        if (context.IsArchived)
        {
            throw Rule("camp_archived", "Archivierte Freizeiten können nicht mehr bearbeitet werden.");
        }
    }

    private Task<KnowledgeCampContext> GetCampContextAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        campContext.GetAsync(
            new KnowledgeCampContextRequest(actorId, organizationId, campId),
            cancellationToken);

    private async Task EnsureCampAccessAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CampAction action,
        CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeCampAsync(
            new CampAccessRequest(actorId, organizationId, campId, action),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("access_denied", "Für diese Aktion fehlt die Berechtigung.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await state.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Rule("concurrency_conflict", "Die Notiz wurde zwischenzeitlich geändert.");
        }
    }

    private static void EnsureActive(NoteEntity note)
    {
        if (note.State != NoteState.Active)
        {
            throw Rule("note_trashed", "Die Notiz befindet sich im Papierkorb.");
        }
    }

    private static void EnsureVersion(long actual, long expected)
    {
        if (actual != expected)
        {
            throw Rule("concurrency_conflict", "Die Notiz wurde zwischenzeitlich geändert.");
        }
    }

    private static string NormalizeDisplayName(
        string value,
        int maxLength,
        string emptyErrorCode,
        string emptyMessage)
    {
        if (value.Any(character => char.IsControl(character) && !char.IsWhiteSpace(character)))
        {
            throw Rule("invalid_text", "Der Text enthält nicht erlaubte Steuerzeichen.");
        }

        var normalized = CollapseWhitespace(value.Normalize(NormalizationForm.FormKC));
        if (normalized.Length == 0)
        {
            throw Rule(emptyErrorCode, emptyMessage);
        }

        if (normalized.Length > maxLength)
        {
            throw Rule("text_too_long", $"Der Text darf höchstens {maxLength} Zeichen lang sein.");
        }

        return normalized;
    }

    private static string NormalizeKey(string value) =>
        CollapseWhitespace(value.Normalize(NormalizationForm.FormKC)).ToUpperInvariant();

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = true;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().TrimEnd();
    }

    private static KnowledgeRuleException InvalidLink() =>
        Rule("invalid_note_link", "Mindestens eine Verknüpfung ist ungültig oder nicht zugänglich.");

    private static KnowledgeRuleException Rule(string code, string message) => new(code, message);
}

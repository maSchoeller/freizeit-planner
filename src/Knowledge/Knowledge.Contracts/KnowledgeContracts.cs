namespace Knowledge.Contracts;

public interface ICampNotebook
{
    Task<IReadOnlyList<NoteSummary>> ListNotesAsync(
        NotebookQuery request,
        CancellationToken cancellationToken);

    Task<Note?> GetNoteAsync(NoteRequest request, CancellationToken cancellationToken);

    Task<Note> CreateNoteAsync(CreateNote request, CancellationToken cancellationToken);

    Task<Note> ReviseNoteAsync(ReviseNote request, CancellationToken cancellationToken);

    Task<Note> MoveNoteToTrashAsync(MoveNoteToTrash request, CancellationToken cancellationToken);

    Task<Note> RestoreNoteAsync(RestoreNote request, CancellationToken cancellationToken);
}

public interface INotebookRetention
{
    Task<NotePurgeResult> PurgeExpiredNotesAsync(int batchSize, CancellationToken cancellationToken);
}

public interface IKnowledgeCampContext
{
    Task<KnowledgeCampContext> GetAsync(
        KnowledgeCampContextRequest request,
        CancellationToken cancellationToken);
}

public interface INoteLinkTargetResolver
{
    Task<IReadOnlyList<ResolvedNoteLink>> ResolveAsync(
        NoteLinkResolutionRequest request,
        CancellationToken cancellationToken);
}

public sealed record NotebookQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    NotebookSection Section = NotebookSection.Active,
    string? Tag = null,
    string? SearchText = null);

public sealed record NoteRequest(Guid ActorId, Guid OrganizationId, Guid CampId, Guid NoteId);

public sealed record KnowledgeCampContextRequest(Guid ActorId, Guid OrganizationId, Guid CampId);

public sealed record KnowledgeCampContext(bool IsArchived);

public sealed record NoteContent(
    string Title,
    string Markdown,
    IReadOnlyList<string> Tags,
    bool IsPinned,
    IReadOnlyList<NoteLinkReference> Links);

public sealed record CreateNote(Guid ActorId, Guid OrganizationId, Guid CampId, NoteContent Content);

public sealed record ReviseNote(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid NoteId,
    long ExpectedVersion,
    NoteContent Content);

public sealed record MoveNoteToTrash(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid NoteId,
    long ExpectedVersion);

public sealed record RestoreNote(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid NoteId,
    long ExpectedVersion);

public sealed record NoteSummary(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Title,
    string PlainTextExcerpt,
    IReadOnlyList<string> Tags,
    bool IsPinned,
    int LinkCount,
    NoteState State,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? TrashedAt,
    DateTimeOffset? PurgeAfter,
    long Version);

public sealed record Note(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Title,
    string Markdown,
    string RenderedHtml,
    IReadOnlyList<string> Tags,
    bool IsPinned,
    IReadOnlyList<NoteLink> Links,
    NoteState State,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    DateTimeOffset? TrashedAt,
    Guid? TrashedBy,
    DateTimeOffset? PurgeAfter,
    long Version);

public sealed record NoteLinkReference(NoteLinkTargetType Type, Guid TargetId);

public sealed record NoteLink(NoteLinkTargetType Type, Guid TargetId, string TargetTitle);

public sealed record NoteLinkResolutionRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    IReadOnlyList<NoteLinkReference> Links);

public sealed record ResolvedNoteLink(NoteLinkTargetType Type, Guid TargetId, string TargetTitle);

public sealed record NotePurgeResult(int PurgedNotes);

public enum NotebookSection
{
    Active,
    Trash
}

public enum NoteState
{
    Active,
    Trashed
}

public enum NoteLinkTargetType
{
    ScheduleEntry,
    Meal,
    Recipe,
    MaterialRequirement,
    ShoppingList,
    Devotion
}

public sealed class KnowledgeRuleException(string errorCode, string message) : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

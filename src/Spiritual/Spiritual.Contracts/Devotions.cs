namespace Spiritual.Contracts;

public interface IDevotionPlanning
{
    Task<IReadOnlyList<DevotionSummary>> ListAsync(
        DevotionScope scope,
        CancellationToken cancellationToken);

    Task<DevotionDetails?> GetAsync(
        DevotionKey key,
        CancellationToken cancellationToken);

    Task<DevotionDetails> CreateAsync(
        CreateDevotion command,
        CancellationToken cancellationToken);

    Task<DevotionDetails> UpdateAsync(
        UpdateDevotion command,
        CancellationToken cancellationToken);

    Task MoveToTrashAsync(
        ChangeDevotionLifecycle command,
        CancellationToken cancellationToken);

    Task RestoreAsync(
        ChangeDevotionLifecycle command,
        CancellationToken cancellationToken);

    Task<BibleSnapshotRefreshResult> RefreshBibleSnapshotAsync(
        RefreshBibleSnapshot command,
        CancellationToken cancellationToken);

    Task<DevotionDetails> SaveManualBibleSnapshotAsync(
        SaveManualBibleSnapshot command,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BibleTranslationView>> ListBibleTranslationsAsync(
        CancellationToken cancellationToken);
}

public sealed record DevotionScope(Guid ActorId, Guid OrganizationId, Guid CampId);

public sealed record DevotionKey(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid DevotionId);

public sealed record CreateDevotion(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    string Topic,
    string BibleReference,
    BibleTranslation Translation,
    string CoreMessage,
    string MarkdownContent,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string MaterialNotes,
    Guid? ScheduleEntryId);

public sealed record UpdateDevotion(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid DevotionId,
    string Topic,
    string BibleReference,
    BibleTranslation Translation,
    string CoreMessage,
    string MarkdownContent,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string MaterialNotes,
    Guid? ScheduleEntryId,
    long ExpectedVersion);

public sealed record ChangeDevotionLifecycle(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid DevotionId,
    long ExpectedVersion);

public sealed record DevotionSummary(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Topic,
    string BibleReference,
    BibleTranslation Translation,
    IReadOnlyList<Guid> ResponsibleUserIds,
    Guid? ScheduleEntryId,
    bool HasBibleSnapshot,
    long Version);

public sealed record DevotionDetails(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Topic,
    string BibleReference,
    BibleTranslation Translation,
    string CoreMessage,
    string MarkdownContent,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string MaterialNotes,
    Guid? ScheduleEntryId,
    BibleSnapshot? BibleSnapshot,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt,
    long Version);

public sealed class SpiritualRuleException(
    string errorCode,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}

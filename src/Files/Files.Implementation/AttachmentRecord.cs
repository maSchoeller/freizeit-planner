using Files.Contracts;

namespace Files.Implementation;

public sealed class AttachmentRecord
{
    public AttachmentRecord(
        Guid id,
        Guid organizationId,
        Guid? campId,
        AttachmentOwnerReference owner,
        AttachmentQuotaScopeType quotaScope,
        string blobName,
        string originalFileName,
        AttachmentMediaType mediaType,
        string contentType,
        long sizeBytes,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        CampId = campId;
        Owner = owner;
        QuotaScope = quotaScope;
        BlobName = blobName;
        OriginalFileName = originalFileName;
        MediaType = mediaType;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public Guid? CampId { get; }

    public AttachmentOwnerReference Owner { get; }

    public AttachmentQuotaScopeType QuotaScope { get; }

    public string BlobName { get; }

    public string OriginalFileName { get; }

    public AttachmentMediaType MediaType { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public Guid CreatedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    public AttachmentLifecycleState State { get; private set; } = AttachmentLifecycleState.PendingUpload;

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset? PurgeAt { get; private set; }

    public long Version { get; private set; } = 1;

    public void MarkAvailable() => State = AttachmentLifecycleState.Available;

    public void MoveToTrash(long expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        if (State != AttachmentLifecycleState.Available)
        {
            throw Rule("attachment_not_available", "Der Anhang kann nicht in den Papierkorb verschoben werden.");
        }
        State = AttachmentLifecycleState.Deleted;
        DeletedAt = now;
        PurgeAt = now.AddDays(30);
        Version++;
    }

    public void Restore(long expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        if (State != AttachmentLifecycleState.Deleted || PurgeAt <= now)
        {
            throw Rule("attachment_restore_expired", "Der Anhang kann nicht mehr wiederhergestellt werden.");
        }
        State = AttachmentLifecycleState.Available;
        DeletedAt = null;
        PurgeAt = null;
        Version++;
    }

    public void RestorePersistenceState(
        AttachmentLifecycleState state,
        DateTimeOffset? deletedAt,
        DateTimeOffset? purgeAt,
        long version)
    {
        State = state;
        DeletedAt = deletedAt;
        PurgeAt = purgeAt;
        Version = version;
    }

    public void EnsureVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw Rule("version_conflict", "Der Anhang wurde zwischenzeitlich geändert. Bitte lade ihn neu.");
        }
    }

    private static FilesRuleException Rule(string code, string message) => new(code, message);
}

public sealed record AttachmentReadGrantRecord(
    Guid Id,
    Guid OrganizationId,
    Guid? CampId,
    Guid AttachmentId,
    Guid ActorId,
    byte[] TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? UsedAt = null);

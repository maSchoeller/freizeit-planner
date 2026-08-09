namespace Files.Contracts;

public interface IAttachmentCatalog
{
    Task<IReadOnlyList<AttachmentView>> ListAsync(
        AttachmentOwnerQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AttachmentView>> ListTrashAsync(
        AttachmentTrashQuery query,
        CancellationToken cancellationToken);

    Task<AttachmentView> UploadAsync(
        UploadAttachment command,
        Stream content,
        CancellationToken cancellationToken);

    Task<AttachmentView> MoveToTrashAsync(
        ChangeAttachmentLifecycle command,
        CancellationToken cancellationToken);

    Task<AttachmentView> RestoreAsync(
        ChangeAttachmentLifecycle command,
        CancellationToken cancellationToken);

    Task<AttachmentQuotaView> GetQuotaAsync(
        AttachmentQuotaQuery query,
        CancellationToken cancellationToken);
}

public interface IAttachmentReader
{
    Task<AttachmentReadGrant> IssueReadGrantAsync(
        AttachmentReadGrantRequest request,
        CancellationToken cancellationToken);

    Task<AttachmentContent> OpenReadAsync(
        OpenAttachmentReadGrant request,
        CancellationToken cancellationToken);
}

public interface IAttachmentMaintenance
{
    Task<AttachmentPurgeResult> PurgeDueAsync(
        int batchSize,
        CancellationToken cancellationToken);

    Task<int> DeleteExpiredReadGrantsAsync(
        int batchSize,
        CancellationToken cancellationToken);
}

public interface IAttachmentOwnerAuthorization
{
    Task<AttachmentOwnerAccessDecision> AuthorizeAsync(
        AttachmentOwnerAccessRequest request,
        CancellationToken cancellationToken);
}

public sealed record AttachmentOwnerReference(AttachmentOwnerType Type, Guid Id);

public sealed record AttachmentOwnerQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid? CampId,
    AttachmentOwnerReference Owner,
    bool IncludeDeleted = false);

public sealed record AttachmentTrashQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId);

public sealed record UploadAttachment(
    Guid ActorId,
    Guid OrganizationId,
    Guid? CampId,
    AttachmentOwnerReference Owner,
    string OriginalFileName,
    string DeclaredContentType,
    long? DeclaredLength);

public sealed record ChangeAttachmentLifecycle(
    Guid ActorId,
    Guid OrganizationId,
    Guid? CampId,
    Guid AttachmentId,
    long ExpectedVersion);

public sealed record AttachmentQuotaQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid? CampId,
    AttachmentQuotaScopeType Scope);

public sealed record AttachmentReadGrantRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid? CampId,
    Guid AttachmentId);

public sealed record OpenAttachmentReadGrant(Guid ActorId, string Token);

public sealed record AttachmentView(
    Guid Id,
    Guid OrganizationId,
    Guid? CampId,
    AttachmentOwnerReference Owner,
    string OriginalFileName,
    AttachmentMediaType MediaType,
    string ContentType,
    long SizeBytes,
    AttachmentLifecycleState State,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeletedAt,
    DateTimeOffset? PurgeAt,
    long Version);

public sealed record AttachmentQuotaView(
    AttachmentQuotaScopeType Scope,
    long LimitBytes,
    long UsedBytes,
    long PendingBytes,
    long AvailableBytes);

public sealed record AttachmentReadGrant(
    string Token,
    Guid AttachmentId,
    DateTimeOffset ExpiresAt,
    AttachmentContentDisposition Disposition);

public sealed record AttachmentPurgeResult(
    int MetadataPurged,
    int BlobsDeleted,
    int RetryableFailures);

public sealed record AttachmentOwnerAccessRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid? CampId,
    AttachmentOwnerReference Owner,
    AttachmentOwnerAction Action);

public sealed record AttachmentOwnerScope(
    Guid OrganizationId,
    Guid? CampId,
    AttachmentQuotaScopeType QuotaScope);

public sealed record AttachmentOwnerAccessDecision(
    bool Allowed,
    AttachmentOwnerScope? Scope)
{
    public static AttachmentOwnerAccessDecision Permit(AttachmentOwnerScope scope) => new(true, scope);

    public static AttachmentOwnerAccessDecision Deny() => new(false, null);
}

public sealed class AttachmentContent(
    Stream content,
    string fileName,
    string contentType,
    long length,
    AttachmentContentDisposition disposition,
    long version) : IAsyncDisposable
{
    public Stream Content { get; } = content;

    public string FileName { get; } = fileName;

    public string ContentType { get; } = contentType;

    public long Length { get; } = length;

    public AttachmentContentDisposition Disposition { get; } = disposition;

    public long Version { get; } = version;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public enum AttachmentOwnerType
{
    ScheduleEntry,
    Meal,
    Recipe,
    MaterialRequirement,
    Devotion,
    Note
}

public enum AttachmentMediaType
{
    Pdf,
    Jpeg,
    Png,
    WebP
}

public enum AttachmentLifecycleState
{
    PendingUpload,
    Available,
    Deleted
}

public enum AttachmentQuotaScopeType
{
    Camp,
    OrganizationRecipeLibrary
}

public enum AttachmentOwnerAction
{
    Read,
    AddAttachment,
    RemoveAttachment,
    RestoreAttachment
}

public enum AttachmentContentDisposition
{
    Inline,
    Attachment
}

public sealed class FilesRuleException(
    string errorCode,
    string message,
    Exception? innerException = null) : InvalidOperationException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}

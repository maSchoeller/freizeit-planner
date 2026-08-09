using Files.Contracts;

namespace Files.Implementation;

public interface IAttachmentState
{
    ValueTask<IReadOnlyList<AttachmentRecord>> ListAsync(
        Guid organizationId,
        Guid? campId,
        AttachmentOwnerReference owner,
        bool includeDeleted,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<AttachmentRecord>> ListTrashAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    ValueTask<AttachmentRecord?> FindAsync(
        Guid organizationId,
        Guid? campId,
        Guid attachmentId,
        CancellationToken cancellationToken);

    ValueTask<bool> TryReserveAsync(
        AttachmentRecord attachment,
        long quotaLimitBytes,
        CancellationToken cancellationToken);

    ValueTask MarkAvailableAsync(AttachmentRecord attachment, CancellationToken cancellationToken);

    ValueTask CancelPendingAsync(AttachmentRecord attachment, CancellationToken cancellationToken);

    ValueTask SaveAsync(AttachmentRecord attachment, CancellationToken cancellationToken);

    ValueTask<AttachmentQuotaUsage> GetQuotaUsageAsync(
        Guid organizationId,
        Guid? campId,
        AttachmentQuotaScopeType scope,
        CancellationToken cancellationToken);

    ValueTask AddReadGrantAsync(AttachmentReadGrantRecord grant, CancellationToken cancellationToken);

    ValueTask<AttachmentReadGrantRecord?> FindReadGrantAsync(
        Guid actorId,
        byte[] tokenHash,
        CancellationToken cancellationToken);

    ValueTask<bool> TryConsumeReadGrantAsync(
        Guid grantId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    ValueTask RevokeReadGrantsAsync(Guid attachmentId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<AttachmentRecord>> ListDueForPurgeAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);

    ValueTask DeletePurgedAsync(AttachmentRecord attachment, CancellationToken cancellationToken);

    ValueTask<int> DeleteExpiredReadGrantsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record AttachmentQuotaUsage(long UsedBytes, long PendingBytes);

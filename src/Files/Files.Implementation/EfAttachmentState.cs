using Files.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Files.Implementation;

public sealed class EfAttachmentState(FilesDbContext dbContext) : IAttachmentState
{
    public async ValueTask<IReadOnlyList<AttachmentRecord>> ListAsync(
        Guid organizationId,
        Guid? campId,
        AttachmentOwnerReference owner,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Attachments.AsNoTracking().Where(item =>
            item.OrganizationId == organizationId
            && item.CampId == campId
            && item.OwnerType == owner.Type
            && item.OwnerId == owner.Id);
        if (!includeDeleted)
        {
            query = query.Where(item => item.State == AttachmentLifecycleState.Available);
        }
        return (await query.OrderBy(item => item.OriginalFileName).ToArrayAsync(cancellationToken))
            .Select(ToRecord)
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<AttachmentRecord>> ListTrashAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        (await dbContext.Attachments
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.State == AttachmentLifecycleState.Deleted)
            .OrderByDescending(item => item.DeletedAt)
            .ThenBy(item => item.OriginalFileName)
            .ToArrayAsync(cancellationToken))
        .Select(ToRecord)
        .ToArray();

    public async ValueTask<AttachmentRecord?> FindAsync(
        Guid organizationId,
        Guid? campId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Attachments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.OrganizationId == organizationId
            && item.CampId == campId
            && item.Id == attachmentId,
            cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async ValueTask<bool> TryReserveAsync(
        AttachmentRecord attachment,
        long quotaLimitBytes,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var scopeKey = $"{attachment.OrganizationId:N}:{attachment.CampId?.ToString("N") ?? "library"}:{attachment.QuotaScope}";
            _ = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({scopeKey}, 0))",
                cancellationToken);
            var used = await dbContext.Attachments
                .Where(item => item.OrganizationId == attachment.OrganizationId
                    && item.CampId == attachment.CampId
                    && item.QuotaScope == attachment.QuotaScope)
                .SumAsync(item => (long?)item.SizeBytes, cancellationToken) ?? 0;
            if (used + attachment.SizeBytes > quotaLimitBytes)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                return false;
            }
            dbContext.Attachments.Add(ToEntity(attachment));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return true;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    public async ValueTask MarkAvailableAsync(
        AttachmentRecord attachment,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.Attachments
            .Where(item => item.Id == attachment.Id && item.State == AttachmentLifecycleState.PendingUpload)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.State, AttachmentLifecycleState.Available),
                cancellationToken);
        if (changed != 1)
        {
            throw Conflict();
        }
    }

    public async ValueTask CancelPendingAsync(
        AttachmentRecord attachment,
        CancellationToken cancellationToken)
    {
        _ = await dbContext.Attachments
            .Where(item => item.Id == attachment.Id && item.State == AttachmentLifecycleState.PendingUpload)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async ValueTask SaveAsync(
        AttachmentRecord attachment,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Attachments.SingleOrDefaultAsync(
            item => item.Id == attachment.Id
                && item.OrganizationId == attachment.OrganizationId
                && item.CampId == attachment.CampId,
            cancellationToken)
            ?? throw new FilesRuleException("attachment_not_found", "Der Anhang wurde nicht gefunden.");
        dbContext.Entry(entity).Property(item => item.Version).OriginalValue = attachment.Version - 1;
        entity.State = attachment.State;
        entity.DeletedAt = attachment.DeletedAt;
        entity.PurgeAt = attachment.PurgeAt;
        entity.Version = attachment.Version;
        await SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<AttachmentQuotaUsage> GetQuotaUsageAsync(
        Guid organizationId,
        Guid? campId,
        AttachmentQuotaScopeType scope,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Attachments.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.QuotaScope == scope)
            .Select(item => new { item.SizeBytes, item.State })
            .ToArrayAsync(cancellationToken);
        return new AttachmentQuotaUsage(
            items.Sum(item => item.SizeBytes),
            items.Where(item => item.State == AttachmentLifecycleState.PendingUpload).Sum(item => item.SizeBytes));
    }

    public async ValueTask AddReadGrantAsync(
        AttachmentReadGrantRecord grant,
        CancellationToken cancellationToken)
    {
        dbContext.ReadGrants.Add(ToEntity(grant));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<AttachmentReadGrantRecord?> FindReadGrantAsync(
        Guid actorId,
        byte[] tokenHash,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ReadGrants.AsNoTracking().SingleOrDefaultAsync(
            item => item.ActorId == actorId && item.TokenHash == tokenHash,
            cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async ValueTask<bool> TryConsumeReadGrantAsync(
        Guid grantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.ReadGrants
            .Where(item => item.Id == grantId && item.UsedAt == null && item.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), cancellationToken);
        return changed == 1;
    }

    public async ValueTask RevokeReadGrantsAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        _ = await dbContext.ReadGrants
            .Where(item => item.AttachmentId == attachmentId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<AttachmentRecord>> ListDueForPurgeAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken) =>
        (await dbContext.Attachments.AsNoTracking()
            .Where(item => item.State == AttachmentLifecycleState.Deleted && item.PurgeAt <= now)
            .OrderBy(item => item.PurgeAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken))
        .Select(ToRecord)
        .ToArray();

    public async ValueTask DeletePurgedAsync(
        AttachmentRecord attachment,
        CancellationToken cancellationToken)
    {
        var deleted = await dbContext.Attachments
            .Where(item => item.Id == attachment.Id
                && item.State == AttachmentLifecycleState.Deleted
                && item.Version == attachment.Version)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted != 1)
        {
            throw Conflict();
        }
    }

    public async ValueTask<int> DeleteExpiredReadGrantsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.ReadGrants.AsNoTracking()
            .Where(item => item.ExpiresAt <= now || item.UsedAt != null)
            .OrderBy(item => item.ExpiresAt)
            .Select(item => item.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        return await dbContext.ReadGrants.Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new FilesRuleException(
                "version_conflict",
                "Der Anhang wurde zwischenzeitlich geändert. Bitte lade ihn neu.",
                exception);
        }
    }

    private static AttachmentRecord ToRecord(AttachmentEntity entity)
    {
        var record = new AttachmentRecord(
            entity.Id,
            entity.OrganizationId,
            entity.CampId,
            new AttachmentOwnerReference(entity.OwnerType, entity.OwnerId),
            entity.QuotaScope,
            entity.BlobName,
            entity.OriginalFileName,
            entity.MediaType,
            entity.ContentType,
            entity.SizeBytes,
            entity.CreatedBy,
            entity.CreatedAt);
        record.RestorePersistenceState(entity.State, entity.DeletedAt, entity.PurgeAt, entity.Version);
        return record;
    }

    private static AttachmentEntity ToEntity(AttachmentRecord record) => new()
    {
        Id = record.Id,
        OrganizationId = record.OrganizationId,
        CampId = record.CampId,
        OwnerType = record.Owner.Type,
        OwnerId = record.Owner.Id,
        QuotaScope = record.QuotaScope,
        BlobName = record.BlobName,
        OriginalFileName = record.OriginalFileName,
        MediaType = record.MediaType,
        ContentType = record.ContentType,
        SizeBytes = record.SizeBytes,
        CreatedBy = record.CreatedBy,
        CreatedAt = record.CreatedAt,
        State = record.State,
        DeletedAt = record.DeletedAt,
        PurgeAt = record.PurgeAt,
        Version = record.Version
    };

    private static AttachmentReadGrantRecord ToRecord(AttachmentReadGrantEntity entity) => new(
        entity.Id,
        entity.OrganizationId,
        entity.CampId,
        entity.AttachmentId,
        entity.ActorId,
        entity.TokenHash,
        entity.CreatedAt,
        entity.ExpiresAt,
        entity.UsedAt);

    private static AttachmentReadGrantEntity ToEntity(AttachmentReadGrantRecord record) => new()
    {
        Id = record.Id,
        OrganizationId = record.OrganizationId,
        CampId = record.CampId,
        AttachmentId = record.AttachmentId,
        ActorId = record.ActorId,
        TokenHash = record.TokenHash,
        CreatedAt = record.CreatedAt,
        ExpiresAt = record.ExpiresAt,
        UsedAt = record.UsedAt
    };

    private static FilesRuleException Conflict() => new(
        "version_conflict",
        "Der Anhang wurde zwischenzeitlich geändert. Bitte lade ihn neu.");
}

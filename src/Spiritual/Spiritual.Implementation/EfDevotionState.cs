using Microsoft.EntityFrameworkCore;
using Spiritual.Contracts;

namespace Spiritual.Implementation;

public sealed class EfDevotionState(SpiritualDbContext dbContext) : IDevotionState
{
    public async ValueTask<IReadOnlyList<DevotionRecord>> ListAsync(
        Guid organizationId,
        Guid campId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Devotions
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.CampId == campId);
        if (!includeDeleted)
        {
            query = query.Where(item => item.DeletedAt == null);
        }
        var entities = await query.OrderBy(item => item.Topic).ToArrayAsync(cancellationToken);
        return await MapAsync(entities, cancellationToken);
    }

    public async ValueTask<DevotionRecord?> FindAsync(
        Guid organizationId,
        Guid campId,
        Guid devotionId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Devotions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId
                    && item.CampId == campId
                    && item.Id == devotionId,
                cancellationToken);
        if (entity is null)
        {
            return null;
        }
        var snapshot = await FindSnapshotAsync(entity.CurrentBibleSnapshotId, cancellationToken);
        return ToRecord(entity, snapshot);
    }

    public async ValueTask AddAsync(
        DevotionRecord devotion,
        CancellationToken cancellationToken)
    {
        dbContext.Devotions.Add(ToEntity(devotion));
        await SaveChangesAsync(cancellationToken);
    }

    public async ValueTask SaveAsync(
        DevotionRecord devotion,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Devotions.SingleOrDefaultAsync(
            item => item.OrganizationId == devotion.OrganizationId
                && item.CampId == devotion.CampId
                && item.Id == devotion.Id,
            cancellationToken)
            ?? throw Rule("devotion_not_found", "Die Andacht wurde nicht gefunden.");
        var originalVersion = devotion.Version - 1;
        dbContext.Entry(entity).Property(item => item.Version).OriginalValue = originalVersion;
        Apply(devotion, entity);

        var storedSnapshot = await FindSnapshotAsync(entity.CurrentBibleSnapshotId, cancellationToken);
        if (devotion.BibleSnapshot is { } snapshot && snapshot != storedSnapshot)
        {
            var snapshotEntity = ToEntity(devotion, snapshot);
            dbContext.BibleSnapshots.Add(snapshotEntity);
            entity.CurrentBibleSnapshotId = snapshotEntity.Id;
        }
        await SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<int> PurgeDueAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.Devotions
            .AsNoTracking()
            .Where(item => item.DeletedAt != null && item.DeletedAt <= cutoff)
            .OrderBy(item => item.DeletedAt)
            .ThenBy(item => item.Id)
            .Take(batchSize)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (ids.Length == 0)
        {
            return 0;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Devotions
            .Where(item => ids.Contains(item.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.CurrentBibleSnapshotId, (Guid?)null),
                cancellationToken);
        await dbContext.BibleSnapshots
            .Where(item => ids.Contains(item.DevotionId))
            .ExecuteDeleteAsync(cancellationToken);
        var deleted = await dbContext.Devotions
            .Where(item => ids.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    private async Task<IReadOnlyList<DevotionRecord>> MapAsync(
        IReadOnlyList<DevotionEntity> entities,
        CancellationToken cancellationToken)
    {
        var snapshotIds = entities
            .Where(item => item.CurrentBibleSnapshotId is not null)
            .Select(item => item.CurrentBibleSnapshotId!.Value)
            .ToArray();
        var snapshots = snapshotIds.Length == 0
            ? new Dictionary<Guid, BibleSnapshot>()
            : await dbContext.BibleSnapshots
                .AsNoTracking()
                .Where(item => snapshotIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, ToSnapshot, cancellationToken);
        return entities
            .Select(item => ToRecord(
                item,
                item.CurrentBibleSnapshotId is { } id ? snapshots.GetValueOrDefault(id) : null))
            .ToArray();
    }

    private async Task<BibleSnapshot?> FindSnapshotAsync(
        Guid? snapshotId,
        CancellationToken cancellationToken)
    {
        if (snapshotId is null)
        {
            return null;
        }
        var entity = await dbContext.BibleSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == snapshotId.Value, cancellationToken);
        return entity is null ? null : ToSnapshot(entity);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new SpiritualRuleException(
                "version_conflict",
                "Die Andacht wurde zwischenzeitlich geändert. Bitte lade sie neu.",
                exception);
        }
    }

    private static DevotionRecord ToRecord(
        DevotionEntity entity,
        BibleSnapshot? snapshot)
    {
        var record = new DevotionRecord(
            entity.Id,
            entity.OrganizationId,
            entity.CampId,
            entity.Topic,
            entity.BibleReference,
            entity.Translation,
            entity.CoreMessage,
            entity.MarkdownContent,
            entity.ResponsibleUserIds,
            entity.MaterialNotes,
            entity.ScheduleEntryId,
            entity.CreatedAt);
        record.RestorePersistenceState(snapshot, entity.UpdatedAt, entity.DeletedAt, entity.Version);
        return record;
    }

    private static DevotionEntity ToEntity(DevotionRecord devotion) => new()
    {
        Id = devotion.Id,
        OrganizationId = devotion.OrganizationId,
        CampId = devotion.CampId,
        Topic = devotion.Topic,
        BibleReference = devotion.BibleReference,
        Translation = devotion.Translation,
        CoreMessage = devotion.CoreMessage,
        MarkdownContent = devotion.MarkdownContent,
        ResponsibleUserIds = devotion.ResponsibleUserIds.ToArray(),
        MaterialNotes = devotion.MaterialNotes,
        ScheduleEntryId = devotion.ScheduleEntryId,
        CreatedAt = devotion.CreatedAt,
        UpdatedAt = devotion.UpdatedAt,
        DeletedAt = devotion.DeletedAt,
        Version = devotion.Version
    };

    private static BibleSnapshotEntity ToEntity(
        DevotionRecord devotion,
        BibleSnapshot snapshot) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = devotion.OrganizationId,
            CampId = devotion.CampId,
            DevotionId = devotion.Id,
            Reference = snapshot.Reference,
            TextExcerpt = snapshot.TextExcerpt,
            TechnicalTranslationId = snapshot.TechnicalTranslationId,
            TranslationDisplayName = snapshot.TranslationDisplayName,
            License = snapshot.License,
            Attribution = snapshot.Attribution,
            RetrievedAt = snapshot.RetrievedAt,
            Origin = snapshot.Origin
        };

    private static void Apply(DevotionRecord devotion, DevotionEntity entity)
    {
        entity.Topic = devotion.Topic;
        entity.BibleReference = devotion.BibleReference;
        entity.Translation = devotion.Translation;
        entity.CoreMessage = devotion.CoreMessage;
        entity.MarkdownContent = devotion.MarkdownContent;
        entity.ResponsibleUserIds = devotion.ResponsibleUserIds.ToArray();
        entity.MaterialNotes = devotion.MaterialNotes;
        entity.ScheduleEntryId = devotion.ScheduleEntryId;
        entity.UpdatedAt = devotion.UpdatedAt;
        entity.DeletedAt = devotion.DeletedAt;
        entity.Version = devotion.Version;
    }

    private static BibleSnapshot ToSnapshot(BibleSnapshotEntity entity) => new(
        entity.Reference,
        entity.TextExcerpt,
        entity.TechnicalTranslationId,
        entity.TranslationDisplayName,
        entity.License,
        entity.Attribution,
        entity.RetrievedAt,
        entity.Origin);

    private static SpiritualRuleException Rule(string code, string message) => new(code, message);
}

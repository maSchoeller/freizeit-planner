using Camps.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Camps.Implementation;

public sealed class EfCampsState(CampsDbContext dbContext) : ICampsState
{
    public async ValueTask<IReadOnlyList<CampRecord>> ListCampsAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        (await dbContext.Camps
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .ToArrayAsync(cancellationToken))
        .Select(ToRecord)
        .ToArray();

    public async ValueTask<CampRecord?> FindCampAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Camps
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.OrganizationId == organizationId && item.Id == campId,
                cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async ValueTask<CampRecord?> FindCampBySlugAsync(
        Guid organizationId,
        string slug,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Camps
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.OrganizationId == organizationId && item.Slug == slug,
                cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async ValueTask AddCampAsync(CampRecord camp, CancellationToken cancellationToken)
    {
        dbContext.Camps.Add(ToEntity(camp));
        await SaveChangesAsync(cancellationToken);
    }

    public async ValueTask SaveCampAsync(
        CampRecord camp,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(camp);
        dbContext.Camps.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        dbContext.Entry(entity).Property(item => item.Version).OriginalValue = expectedVersion;
        await SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<ScheduleEntryRecord>> ListScheduleEntriesAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var entries = await dbContext.ScheduleEntries
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.DeletedAt == null)
            .ToArrayAsync(cancellationToken);
        var entryIds = entries.Select(item => item.Id).ToArray();
        var responsibilities = await dbContext.ScheduleResponsibilities
            .AsNoTracking()
            .Where(item => entryIds.Contains(item.ScheduleEntryId))
            .ToArrayAsync(cancellationToken);
        return entries.Select(item => ToRecord(
            item,
            responsibilities
                .Where(responsibility => responsibility.ScheduleEntryId == item.Id)
                .Select(responsibility => responsibility.UserId)
                .ToArray()))
            .ToArray();
    }

    public async ValueTask<ScheduleEntryRecord?> FindScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ScheduleEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.OrganizationId == organizationId
                && item.CampId == campId
                && item.Id == scheduleEntryId
                && item.DeletedAt == null,
                cancellationToken);
        if (entity is null) return null;
        var responsibilities = await dbContext.ScheduleResponsibilities
            .AsNoTracking()
            .Where(item => item.ScheduleEntryId == scheduleEntryId)
            .Select(item => item.UserId)
            .ToArrayAsync(cancellationToken);
        return ToRecord(entity, responsibilities);
    }

    public async ValueTask AddScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        CancellationToken cancellationToken)
    {
        dbContext.ScheduleEntries.Add(ToEntity(scheduleEntry));
        dbContext.ScheduleResponsibilities.AddRange(ToResponsibilityEntities(scheduleEntry));
        await SaveChangesAsync(cancellationToken);
    }

    public async ValueTask SaveScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(scheduleEntry);
        dbContext.ScheduleEntries.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        dbContext.Entry(entity).Property(item => item.Version).OriginalValue = expectedVersion;
        var current = await dbContext.ScheduleResponsibilities
            .Where(item => item.ScheduleEntryId == scheduleEntry.Id)
            .ToArrayAsync(cancellationToken);
        var requestedUserIds = scheduleEntry.ResponsibleUserIds.ToHashSet();
        dbContext.ScheduleResponsibilities.RemoveRange(
            current.Where(item => !requestedUserIds.Contains(item.UserId)));
        var currentUserIds = current.Select(item => item.UserId).ToHashSet();
        dbContext.ScheduleResponsibilities.AddRange(
            ToResponsibilityEntities(scheduleEntry)
                .Where(item => !currentUserIds.Contains(item.UserId)));
        await SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DeleteScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(scheduleEntry);
        dbContext.ScheduleEntries.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        dbContext.Entry(entity).Property(item => item.Version).OriginalValue = expectedVersion;
        await SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<ScheduleEntryRecord>> ListDeletedScheduleEntriesAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var entries = await dbContext.ScheduleEntries
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.DeletedAt != null)
            .ToArrayAsync(cancellationToken);
        var entryIds = entries.Select(item => item.Id).ToArray();
        var responsibilities = await dbContext.ScheduleResponsibilities
            .AsNoTracking()
            .Where(item => entryIds.Contains(item.ScheduleEntryId))
            .ToArrayAsync(cancellationToken);
        return entries.Select(item => ToRecord(
            item,
            responsibilities
                .Where(responsibility => responsibility.ScheduleEntryId == item.Id)
                .Select(responsibility => responsibility.UserId)
                .ToArray()))
            .ToArray();
    }

    public async ValueTask<ScheduleEntryRecord?> FindDeletedScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ScheduleEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.Id == scheduleEntryId
                && item.DeletedAt != null,
                cancellationToken);
        if (entity is null) return null;
        var responsibilities = await dbContext.ScheduleResponsibilities
            .AsNoTracking()
            .Where(item => item.ScheduleEntryId == scheduleEntryId)
            .Select(item => item.UserId)
            .ToArrayAsync(cancellationToken);
        return ToRecord(entity, responsibilities);
    }

    public async ValueTask<int> PurgeDueScheduleEntriesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken) =>
        await dbContext.ScheduleEntries
            .Where(item => item.PurgeAt != null && item.PurgeAt <= now)
            .OrderBy(item => item.PurgeAt)
            .ThenBy(item => item.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CampsRuleException(
                "version_conflict",
                "Der Datensatz wurde zwischenzeitlich geändert.",
                exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new CampsRuleException(
                "camp_slug_conflict",
                "Dieser Camp-Link ist bereits vergeben.",
                exception);
        }
    }

    private static CampRecord ToRecord(CampEntity entity) => new(
        entity.Id,
        entity.OrganizationId,
        entity.Name,
        entity.Slug,
        entity.Description,
        entity.StartsOn,
        entity.EndsOn,
        entity.TimeZoneId,
        entity.DefaultPortions,
        entity.Status,
        entity.Version);

    private static CampEntity ToEntity(CampRecord record) => new()
    {
        Id = record.Id,
        OrganizationId = record.OrganizationId,
        Name = record.Name,
        Slug = record.Slug,
        Description = record.Description,
        StartsOn = record.StartsOn,
        EndsOn = record.EndsOn,
        TimeZoneId = record.TimeZoneId,
        DefaultPortions = record.DefaultPortions,
        Status = record.Status,
        Version = record.Version
    };

    private static ScheduleEntryRecord ToRecord(
        ScheduleEntryEntity entity,
        IReadOnlyList<Guid> responsibilityUserIds) => new(
        entity.Id,
        entity.OrganizationId,
        entity.CampId,
        new ScheduleTimingRecord(
            entity.IsAllDay,
            entity.StartsAtUtc,
            entity.EndsAtUtc,
            entity.StartDate,
            entity.EndDateExclusive),
        entity.Title,
        entity.Description,
        entity.Location,
        entity.Category,
        entity.Status,
        responsibilityUserIds,
        entity.Audience,
        entity.Version,
        entity.DeletedAt,
        entity.PurgeAt);

    private static ScheduleEntryEntity ToEntity(ScheduleEntryRecord record) => new()
    {
        Id = record.Id,
        OrganizationId = record.OrganizationId,
        CampId = record.CampId,
        IsAllDay = record.Timing.IsAllDay,
        StartsAtUtc = record.Timing.StartsAtUtc,
        EndsAtUtc = record.Timing.EndsAtUtc,
        StartDate = record.Timing.StartDate,
        EndDateExclusive = record.Timing.EndDateExclusive,
        Title = record.Title,
        Description = record.Description,
        Location = record.Location,
        Category = record.Category,
        Status = record.Status,
        Audience = record.Audience,
        Version = record.Version,
        DeletedAt = record.DeletedAt,
        PurgeAt = record.PurgeAt
    };

    private static IEnumerable<ScheduleResponsibilityEntity> ToResponsibilityEntities(
        ScheduleEntryRecord record) => record.ResponsibleUserIds.Select(userId => new ScheduleResponsibilityEntity
        {
            ScheduleEntryId = record.Id,
            UserId = userId,
            OrganizationId = record.OrganizationId,
            CampId = record.CampId
        });
}

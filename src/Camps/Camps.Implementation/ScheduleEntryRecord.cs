using Camps.Contracts;

namespace Camps.Implementation;

public sealed class ScheduleEntryRecord
{
    public ScheduleEntryRecord(
        Guid id,
        Guid organizationId,
        Guid campId,
        ScheduleTimingRecord timing,
        string title,
        string? description,
        string? location,
        string category,
        ScheduleEntryStatus status,
        IReadOnlyList<Guid> responsibleUserIds,
        string? audience,
        long version = 1,
        DateTimeOffset? deletedAt = null,
        DateTimeOffset? purgeAt = null)
    {
        Id = id;
        OrganizationId = organizationId;
        CampId = campId;
        Timing = timing;
        Title = title;
        Description = description;
        Location = location;
        Category = category;
        Status = status;
        ResponsibleUserIds = responsibleUserIds.ToArray();
        Audience = audience;
        Version = version;
        DeletedAt = deletedAt;
        PurgeAt = purgeAt;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public Guid CampId { get; }

    public ScheduleTimingRecord Timing { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public string? Location { get; private set; }

    public string Category { get; private set; }

    public ScheduleEntryStatus Status { get; private set; }

    public IReadOnlyList<Guid> ResponsibleUserIds { get; private set; }

    public string? Audience { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset? PurgeAt { get; private set; }

    public void Update(
        ScheduleTimingRecord timing,
        string title,
        string? description,
        string? location,
        string category,
        ScheduleEntryStatus status,
        IReadOnlyList<Guid> responsibleUserIds,
        string? audience,
        long expectedVersion)
    {
        RequireVersion(expectedVersion);
        Timing = timing;
        Title = title;
        Description = description;
        Location = location;
        Category = category;
        Status = status;
        ResponsibleUserIds = responsibleUserIds.ToArray();
        Audience = audience;
        Version++;
    }

    public void RequireVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new CampsRuleException(
                "version_conflict",
                "Der Zeitplaneintrag wurde zwischenzeitlich geändert.");
        }
    }

    public void MoveToTrash(long expectedVersion, DateTimeOffset now)
    {
        RequireVersion(expectedVersion);
        if (DeletedAt is not null)
        {
            throw new CampsRuleException(
                "schedule_entry_already_trashed",
                "Der Zeitplaneintrag befindet sich bereits im Papierkorb.");
        }
        DeletedAt = now;
        PurgeAt = now.AddDays(30);
        Version++;
    }

    public void Restore(long expectedVersion, DateTimeOffset now)
    {
        RequireVersion(expectedVersion);
        if (DeletedAt is null || PurgeAt is null)
        {
            throw new CampsRuleException(
                "schedule_entry_not_trashed",
                "Der Zeitplaneintrag befindet sich nicht im Papierkorb.");
        }
        if (PurgeAt <= now)
        {
            throw new CampsRuleException(
                "schedule_entry_restore_expired",
                "Die Aufbewahrungsfrist ist abgelaufen.");
        }
        DeletedAt = null;
        PurgeAt = null;
        Version++;
    }
}

public sealed record ScheduleTimingRecord(
    bool IsAllDay,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateOnly? StartDate,
    DateOnly? EndDateExclusive);

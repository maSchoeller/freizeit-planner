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
        long version = 1)
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
}

public sealed record ScheduleTimingRecord(
    bool IsAllDay,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateOnly? StartDate,
    DateOnly? EndDateExclusive);

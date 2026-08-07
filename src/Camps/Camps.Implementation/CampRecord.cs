using Camps.Contracts;

namespace Camps.Implementation;

public sealed class CampRecord
{
    public CampRecord(
        Guid id,
        Guid organizationId,
        string name,
        string slug,
        string? description,
        DateOnly startsOn,
        DateOnly endsOn,
        string timeZoneId,
        int defaultPortions,
        CampStatus status = CampStatus.Active,
        long version = 1)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        Slug = slug;
        Description = description;
        StartsOn = startsOn;
        EndsOn = endsOn;
        TimeZoneId = timeZoneId;
        DefaultPortions = defaultPortions;
        Status = status;
        Version = version;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public string? Description { get; private set; }

    public DateOnly StartsOn { get; private set; }

    public DateOnly EndsOn { get; private set; }

    public string TimeZoneId { get; private set; }

    public int DefaultPortions { get; private set; }

    public CampStatus Status { get; private set; }

    public long Version { get; private set; }

    public void Update(
        string name,
        string slug,
        string? description,
        DateOnly startsOn,
        DateOnly endsOn,
        string timeZoneId,
        int defaultPortions,
        long expectedVersion)
    {
        RequireVersion(expectedVersion);
        Name = name;
        Slug = slug;
        Description = description;
        StartsOn = startsOn;
        EndsOn = endsOn;
        TimeZoneId = timeZoneId;
        DefaultPortions = defaultPortions;
        Version++;
    }

    public void ChangeStatus(CampStatus status, long expectedVersion)
    {
        RequireVersion(expectedVersion);
        Status = status;
        Version++;
    }

    private void RequireVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new CampsRuleException(
                "version_conflict",
                "Das Camp wurde zwischenzeitlich geändert.");
        }
    }
}

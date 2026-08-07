namespace Camps.Contracts;

public interface ICampManagement
{
    Task<IReadOnlyList<CampSummary>> ListAsync(
        CampListQuery query,
        CancellationToken cancellationToken);

    Task<CampView> GetBySlugAsync(
        CampBySlugQuery query,
        CancellationToken cancellationToken);

    Task<CampView> CreateAsync(
        CreateCamp command,
        CancellationToken cancellationToken);

    Task<CampView> UpdateAsync(
        UpdateCamp command,
        CancellationToken cancellationToken);

    Task<CampView> ChangeStatusAsync(
        ChangeCampStatus command,
        CancellationToken cancellationToken);
}

public interface ICampPlanningDefaults
{
    Task<CampPlanningDefaults> GetAsync(
        CampAccessQuery query,
        CancellationToken cancellationToken);
}

public sealed record CampListQuery(Guid ActorId, Guid OrganizationId);

public sealed record CampBySlugQuery(Guid ActorId, Guid OrganizationId, string CampSlug);

public sealed record CampAccessQuery(Guid ActorId, Guid OrganizationId, Guid CampId);

public sealed record CreateCamp(
    Guid ActorId,
    Guid OrganizationId,
    string Name,
    string Slug,
    string? Description,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string? TimeZoneId,
    int DefaultPortions);

public sealed record UpdateCamp(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    string Slug,
    string? Description,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string TimeZoneId,
    int DefaultPortions,
    long ExpectedVersion);

public sealed record ChangeCampStatus(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    CampStatus Status,
    long ExpectedVersion);

public sealed record CampSummary(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Slug,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string TimeZoneId,
    int DefaultPortions,
    CampStatus Status,
    CampPeriod Period,
    long Version);

public sealed record CampView(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Slug,
    string? Description,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string TimeZoneId,
    int DefaultPortions,
    CampStatus Status,
    CampPeriod Period,
    long Version);

public sealed record CampPlanningDefaults(
    Guid CampId,
    int DefaultPortions,
    CampStatus Status,
    long Version);

public enum CampStatus
{
    Active,
    Archived
}

public enum CampPeriod
{
    Upcoming,
    Ongoing,
    Past
}

public sealed class CampsRuleException(
    string errorCode,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}

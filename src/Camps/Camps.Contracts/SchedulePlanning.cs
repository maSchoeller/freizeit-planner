namespace Camps.Contracts;

public interface ISchedulePlanning
{
    Task<IReadOnlyList<ScheduleEntryView>> ListAsync(
        ScheduleRangeQuery query,
        CancellationToken cancellationToken);

    Task<ScheduleEntryView> GetAsync(
        ScheduleEntryQuery query,
        CancellationToken cancellationToken);

    Task<ScheduleEntryView> CreateAsync(
        CreateScheduleEntry command,
        CancellationToken cancellationToken);

    Task<ScheduleEntryView> UpdateAsync(
        UpdateScheduleEntry command,
        CancellationToken cancellationToken);

    Task<ScheduleEntryReference> DeleteAsync(
        DeleteScheduleEntry command,
        CancellationToken cancellationToken);
}

public interface IScheduleReferenceAccess
{
    Task<ScheduleEntryReference> RequireAsync(
        ScheduleEntryReferenceRequest request,
        CancellationToken cancellationToken);
}

public sealed record ScheduleRangeQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    DateOnly FromDate,
    DateOnly ToDateExclusive);

public sealed record ScheduleEntryQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ScheduleEntryId);

public sealed record CreateScheduleEntry(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    ScheduleTimingInput Timing,
    string Title,
    string? Description,
    string? Location,
    string Category,
    ScheduleEntryStatus Status,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string? Audience);

public sealed record UpdateScheduleEntry(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ScheduleEntryId,
    ScheduleTimingInput Timing,
    string Title,
    string? Description,
    string? Location,
    string Category,
    ScheduleEntryStatus Status,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string? Audience,
    long ExpectedVersion);

public sealed record DeleteScheduleEntry(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ScheduleEntryId,
    long ExpectedVersion);

public sealed record ScheduleTimingInput(
    bool IsAllDay,
    DateTime? LocalStart,
    DateTime? LocalEnd,
    DateOnly? StartDate,
    DateOnly? EndDateExclusive,
    AmbiguousLocalTimeChoice StartChoice = AmbiguousLocalTimeChoice.Reject,
    AmbiguousLocalTimeChoice EndChoice = AmbiguousLocalTimeChoice.Reject);

public sealed record ScheduleTimingView(
    bool IsAllDay,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateOnly? StartDate,
    DateOnly? EndDateExclusive,
    string TimeZoneId);

public sealed record ScheduleEntryView(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    ScheduleTimingView Timing,
    string Title,
    string? Description,
    string? Location,
    string Category,
    ScheduleEntryStatus Status,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string? Audience,
    bool OverlapsAnotherEntry,
    long Version);

public sealed record ScheduleEntryReference(
    Guid OrganizationId,
    Guid CampId,
    Guid ScheduleEntryId,
    long Version);

public sealed record ScheduleEntryReferenceRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ScheduleEntryId,
    ScheduleReferencePurpose Purpose);

public enum ScheduleEntryStatus
{
    Planned,
    Confirmed,
    Cancelled
}

public enum AmbiguousLocalTimeChoice
{
    Reject,
    EarlierOffset,
    LaterOffset
}

public enum ScheduleReferencePurpose
{
    Read,
    LinkForWrite
}

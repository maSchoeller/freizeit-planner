namespace Logistics.Contracts;

public interface IMaterialPlanning
{
    Task<IReadOnlyList<MaterialRequirementSummary>> ListAsync(
        MaterialQuery query,
        CancellationToken cancellationToken);

    Task<MaterialRequirement?> GetAsync(
        MaterialRequest request,
        CancellationToken cancellationToken);

    Task<MaterialRequirement> CreateAsync(
        CreateMaterialRequirement command,
        CancellationToken cancellationToken);

    Task<MaterialRequirement> UpdateAsync(
        UpdateMaterialRequirement command,
        CancellationToken cancellationToken);

    Task DeleteAsync(DeleteMaterialRequirement command, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrashedMaterialRequirement>> ListTrashAsync(
        MaterialTrashQuery query,
        CancellationToken cancellationToken);

    Task<MaterialRequirement> RestoreAsync(
        RestoreMaterialRequirement command,
        CancellationToken cancellationToken);
}

public sealed record MaterialQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid? ScheduleEntryId = null,
    ProcurementStatus? Status = null);

public sealed record MaterialRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid MaterialRequirementId);

public sealed record CreateMaterialRequirement(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    string? Description,
    LogisticsQuantity Quantity,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string? ProcurementSource,
    string? Note,
    ProcurementStatus Status,
    Guid? ScheduleEntryId);

public sealed record UpdateMaterialRequirement(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid MaterialRequirementId,
    string Name,
    string? Description,
    LogisticsQuantity Quantity,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string? ProcurementSource,
    string? Note,
    ProcurementStatus Status,
    Guid? ScheduleEntryId,
    long ExpectedVersion);

public sealed record DeleteMaterialRequirement(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid MaterialRequirementId,
    long ExpectedVersion);

public sealed record MaterialTrashQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId);

public sealed record RestoreMaterialRequirement(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid MaterialRequirementId,
    long ExpectedVersion);

public sealed record TrashedMaterialRequirement(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    DateTimeOffset DeletedAt,
    DateTimeOffset PurgeAt,
    long Version);

public sealed record MaterialRequirementSummary(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    LogisticsQuantity Quantity,
    ProcurementStatus Status,
    Guid? ScheduleEntryId,
    long Version);

public sealed record MaterialRequirement(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    string? Description,
    LogisticsQuantity Quantity,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string? ProcurementSource,
    string? Note,
    ProcurementStatus Status,
    Guid? ScheduleEntryId,
    long Version);

public enum ProcurementStatus
{
    Open,
    Planned,
    Procured,
    NotRequired
}

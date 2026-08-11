namespace Identity.Contracts;

public interface ITenantAccessControl
{
    Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken);

    Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken);
}

public interface ITenantAdministration
{
    Task<IReadOnlyList<OrganizationMemberView>> ListOrganizationMembersAsync(
        Guid actorId,
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<OrganizationMemberView> ChangeOrganizationRoleAsync(
        OrganizationRoleChange change,
        CancellationToken cancellationToken);

    Task RemoveOrganizationMemberAsync(
        OrganizationMemberRemoval removal,
        CancellationToken cancellationToken);

    Task<CampAssignmentView> AssignCampMemberAsync(
        CampMemberAssignment assignment,
        CancellationToken cancellationToken);

    Task RemoveCampMemberAsync(
        CampMemberRemoval removal,
        CancellationToken cancellationToken);
}

public interface ICampMemberDirectory
{
    Task<IReadOnlyList<CampMemberSummary>> ListCampMembersAsync(
        CampMemberDirectoryQuery query,
        CancellationToken cancellationToken);
}

public interface ISuperAdminOrganizationAdministration
{
    Task<IReadOnlyList<SuperAdminOrganizationView>> ListOrganizationsAsync(
        Guid actorId,
        CancellationToken cancellationToken);

    Task<OrganizationStatusView> ChangeOrganizationStatusAsync(
        OrganizationStatusChange change,
        CancellationToken cancellationToken);
}

public sealed record OrganizationAccessRequest(
    Guid ActorId,
    Guid OrganizationId,
    OrganizationAction Action);

public sealed record CampAccessRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    CampAction Action);

public sealed record TenantAccessDecision(
    bool Allowed,
    TenantAccessDenial Denial,
    TenantRole? EffectiveRole)
{
    public static TenantAccessDecision Permit(TenantRole role) =>
        new(true, TenantAccessDenial.None, role);

    public static TenantAccessDecision Deny(TenantAccessDenial denial) =>
        new(false, denial, null);
}

public sealed record OrganizationRoleChange(
    Guid ActorId,
    Guid OrganizationId,
    Guid UserId,
    TenantRole Role,
    long ExpectedVersion);

public sealed record OrganizationMemberRemoval(
    Guid ActorId,
    Guid OrganizationId,
    Guid UserId,
    long ExpectedVersion);

public sealed record CampMemberAssignment(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid UserId,
    TenantRole Role,
    long? ExpectedVersion);

public sealed record CampMemberRemoval(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid UserId,
    long ExpectedVersion);

public sealed record CampMemberDirectoryQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId);

public sealed record CampMemberSummary(Guid UserId, string DisplayName);

public sealed record OrganizationStatusChange(
    Guid ActorId,
    Guid OrganizationId,
    OrganizationStatus Status,
    long ExpectedVersion);

public sealed record OrganizationMemberView(
    Guid UserId,
    TenantRole Role,
    bool IsActive,
    long Version,
    string? Email = null,
    string? DisplayName = null);

public sealed record CampAssignmentView(
    Guid UserId,
    Guid CampId,
    TenantRole Role,
    bool IsActive,
    long Version);

public sealed record OrganizationStatusView(
    Guid OrganizationId,
    OrganizationStatus Status,
    long Version);

public sealed record SuperAdminOrganizationView(
    Guid OrganizationId,
    string Name,
    string Slug,
    OrganizationStatus Status,
    long Version);

public enum OrganizationAction
{
    Read,
    ManageCamps,
    ManageInvitations,
    ManageMembers,
    ManageSettings,
    DeleteOrganization,
    Export
}

public enum CampAction
{
    Read,
    WriteContent,
    ManageCamp,
    ManageAssignments,
    Export
}

public enum TenantAccessDenial
{
    None,
    ActorUnknown,
    PlatformScopeOnly,
    OrganizationNotFound,
    OrganizationSuspended,
    MembershipRequired,
    CampAssignmentRequired,
    PermissionDenied
}

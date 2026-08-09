using Identity.Contracts;

namespace Identity.Implementation;

public interface ITenantAuthorizationState
{
    ValueTask<LifecycleUser?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    ValueTask<OrganizationRecord?> FindOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<OrganizationRecord>> ListOrganizationsAsync(
        CancellationToken cancellationToken);

    ValueTask<MembershipRecord?> FindMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    ValueTask<CampAssignmentRecord?> FindCampAssignmentAsync(
        Guid organizationId,
        Guid campId,
        Guid userId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<MembershipRecord>> ListOrganizationMembershipsAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<CampMemberSummary>> ListCampMembersAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    ValueTask<int> CountActiveOwnersAsync(Guid organizationId, CancellationToken cancellationToken);

    ValueTask SaveMembershipAsync(MembershipRecord membership, CancellationToken cancellationToken);

    ValueTask SaveCampAssignmentAsync(
        CampAssignmentRecord assignment,
        CancellationToken cancellationToken);

    ValueTask SaveOrganizationAsync(
        OrganizationRecord organization,
        CancellationToken cancellationToken);
}

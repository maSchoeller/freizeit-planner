namespace Identity.Implementation;

public interface IIdentityLifecycleState
{
    ValueTask<LifecycleUser?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    ValueTask<LifecycleUser?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    ValueTask SaveUserAsync(LifecycleUser user, CancellationToken cancellationToken);

    ValueTask<OrganizationRecord?> FindOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    ValueTask<OrganizationRecord?> FindOrganizationBySlugAsync(
        string slug,
        CancellationToken cancellationToken);

    ValueTask<MembershipRecord?> FindMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<MembershipRecord>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    ValueTask<int> CountActiveOwnersAsync(Guid organizationId, CancellationToken cancellationToken);

    ValueTask SaveMembershipAsync(MembershipRecord membership, CancellationToken cancellationToken);

    ValueTask<InvitationRecord?> FindInvitationAsync(Guid invitationId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<InvitationRecord>> ListInvitationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    ValueTask SaveOrganizationInvitationAsync(
        OrganizationRecord organization,
        InvitationRecord invitation,
        CancellationToken cancellationToken);

    ValueTask SaveInvitationAsync(InvitationRecord invitation, CancellationToken cancellationToken);

    ValueTask<bool> TryAcceptInvitationAsync(
        InvitationRecord invitation,
        LifecycleUser user,
        MembershipRecord membership,
        CampAssignmentRecord? assignment,
        CancellationToken cancellationToken);

    ValueTask SaveOrganizationAsync(OrganizationRecord organization, CancellationToken cancellationToken);

    ValueTask AddInvitationRateEventAsync(RateEvent rateEvent, CancellationToken cancellationToken);

    ValueTask<int> CountInvitationRateEventsAsync(
        string partition,
        DateTimeOffset since,
        CancellationToken cancellationToken);
}

public sealed class LifecycleUser(
    Guid id,
    string email,
    string normalizedEmail,
    string displayName,
    bool isPlatformAdmin = false,
    DateTimeOffset? deletionScheduledAt = null)
{
    public Guid Id { get; } = id;

    public string Email { get; private set; } = email;

    public string NormalizedEmail { get; private set; } = normalizedEmail;

    public string DisplayName { get; private set; } = displayName;

    public bool IsPlatformAdmin { get; } = isPlatformAdmin;

    public DateTimeOffset? DeletionScheduledAt { get; private set; } = deletionScheduledAt;

    public void Rename(string displayName) => DisplayName = displayName;

    public void ScheduleDeletion(DateTimeOffset scheduledAt) => DeletionScheduledAt = scheduledAt;

    public void CancelDeletion() => DeletionScheduledAt = null;
}

public sealed class OrganizationRecord(
    Guid id,
    string name,
    string slug,
    Identity.Contracts.OrganizationStatus status = Identity.Contracts.OrganizationStatus.Active,
    DateTimeOffset? deletionScheduledAt = null,
    long version = 1)
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    public string Slug { get; } = slug;

    public Identity.Contracts.OrganizationStatus Status { get; private set; } = status;

    public DateTimeOffset? DeletionScheduledAt { get; private set; } = deletionScheduledAt;

    public long Version { get; private set; } = version;

    public void ScheduleDeletion(DateTimeOffset scheduledAt)
    {
        DeletionScheduledAt = scheduledAt;
        Version++;
    }

    public void CancelDeletion()
    {
        DeletionScheduledAt = null;
        Version++;
    }
}

public sealed class MembershipRecord(
    Guid organizationId,
    Guid userId,
    Identity.Contracts.TenantRole role,
    bool isActive = true)
{
    public Guid OrganizationId { get; } = organizationId;

    public Guid UserId { get; } = userId;

    public Identity.Contracts.TenantRole Role { get; private set; } = role;

    public bool IsActive { get; private set; } = isActive;

    public void Leave() => IsActive = false;
}

public sealed record CampAssignmentRecord(
    Guid OrganizationId,
    Guid CampId,
    Guid UserId,
    Identity.Contracts.TenantRole Role);

public sealed class InvitationRecord(
    Guid id,
    Guid organizationId,
    string normalizedEmail,
    Identity.Contracts.TenantRole role,
    Guid? campId,
    string tokenHash,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt,
    bool isPlatformInvitation,
    DateTimeOffset? revokedAt = null,
    DateTimeOffset? usedAt = null,
    Guid? rotatedFromId = null)
{
    public Guid Id { get; } = id;

    public Guid OrganizationId { get; } = organizationId;

    public string NormalizedEmail { get; } = normalizedEmail;

    public Identity.Contracts.TenantRole Role { get; } = role;

    public Guid? CampId { get; } = campId;

    public string TokenHash { get; } = tokenHash;

    public DateTimeOffset CreatedAt { get; } = createdAt;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    public bool IsPlatformInvitation { get; } = isPlatformInvitation;

    public DateTimeOffset? RevokedAt { get; private set; } = revokedAt;

    public DateTimeOffset? UsedAt { get; private set; } = usedAt;

    public Guid? RotatedFromId { get; } = rotatedFromId;

    public void Revoke(DateTimeOffset revokedAt) => RevokedAt ??= revokedAt;

    public void MarkUsed(DateTimeOffset usedAt) => UsedAt ??= usedAt;
}

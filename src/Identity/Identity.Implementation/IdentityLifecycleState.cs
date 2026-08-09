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
    DateTimeOffset? deletionScheduledAt = null,
    DateTimeOffset? erasureStartedAt = null)
{
    public Guid Id { get; } = id;

    public string Email { get; private set; } = email;

    public string NormalizedEmail { get; private set; } = normalizedEmail;

    public string DisplayName { get; private set; } = displayName;

    public bool IsPlatformAdmin { get; } = isPlatformAdmin;

    public DateTimeOffset? DeletionScheduledAt { get; private set; } = deletionScheduledAt;

    public DateTimeOffset? ErasureStartedAt { get; } = erasureStartedAt;

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

    public void Suspend() => ChangeStatus(Identity.Contracts.OrganizationStatus.Suspended, Version);

    public void ChangeStatus(Identity.Contracts.OrganizationStatus status, long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Status = status;
        Version++;
    }

    private void EnsureVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new Identity.Contracts.IdentityRuleException(
                "version_conflict",
                "Die Organization wurde zwischenzeitlich geändert.");
        }
    }
}

public sealed class MembershipRecord(
    Guid organizationId,
    Guid userId,
    Identity.Contracts.TenantRole role,
    bool isActive = true,
    long version = 1)
{
    public Guid OrganizationId { get; } = organizationId;

    public Guid UserId { get; } = userId;

    public Identity.Contracts.TenantRole Role { get; private set; } = role;

    public bool IsActive { get; private set; } = isActive;

    public long Version { get; private set; } = version;

    public void Leave() => Remove(Version);

    public void ChangeRole(Identity.Contracts.TenantRole role, long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Role = role;
        Version++;
    }

    public void Remove(long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        IsActive = false;
        Version++;
    }

    private void EnsureVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new Identity.Contracts.IdentityRuleException(
                "version_conflict",
                "Die Mitgliedschaft wurde zwischenzeitlich geändert.");
        }
    }
}

public sealed class CampAssignmentRecord(
    Guid organizationId,
    Guid campId,
    Guid userId,
    Identity.Contracts.TenantRole role,
    bool isActive = true,
    long version = 1)
{
    public Guid OrganizationId { get; } = organizationId;

    public Guid CampId { get; } = campId;

    public Guid UserId { get; } = userId;

    public Identity.Contracts.TenantRole Role { get; private set; } = role;

    public bool IsActive { get; private set; } = isActive;

    public long Version { get; private set; } = version;

    public void Assign(Identity.Contracts.TenantRole role, long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Role = role;
        IsActive = true;
        Version++;
    }

    public void Remove(long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        IsActive = false;
        Version++;
    }

    private void EnsureVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new Identity.Contracts.IdentityRuleException(
                "version_conflict",
                "Die Camp-Zuweisung wurde zwischenzeitlich geändert.");
        }
    }
}

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
    Guid? rotatedFromId = null,
    long version = 1)
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

    public long Version { get; private set; } = version;

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (RevokedAt is null)
        {
            RevokedAt = revokedAt;
            Version++;
        }
    }

    public void MarkUsed(DateTimeOffset usedAt)
    {
        if (UsedAt is null)
        {
            UsedAt = usedAt;
            Version++;
        }
    }
}

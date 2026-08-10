namespace Identity.Contracts;

public interface IInvitationLifecycle
{
    Task<IReadOnlyList<InvitationSummary>> ListInvitationsAsync(
        Guid actorId,
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<IssuedInvitation> CreateOrganizationInvitationAsync(
        OrganizationInvitationRequest request,
        CancellationToken cancellationToken);

    Task<IssuedInvitation> IssueTeamInvitationAsync(
        TeamInvitationRequest request,
        CancellationToken cancellationToken);

    Task<IssuedInvitation> RotateInvitationAsync(
        Guid actorId,
        Guid invitationId,
        CancellationToken cancellationToken);

    Task RevokeInvitationAsync(Guid actorId, Guid invitationId, CancellationToken cancellationToken);

    Task<InvitationAcceptance> AcceptInvitationAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken);
}

public interface IAccountLifecycle
{
    Task<AccountView> GetAccountAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountMembershipView>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<AccountView> UpdateDisplayNameAsync(
        Guid userId,
        string displayName,
        CancellationToken cancellationToken);

    Task<DeletionSchedule> ScheduleAccountDeletionAsync(Guid userId, CancellationToken cancellationToken);

    Task CancelAccountDeletionAsync(Guid userId, CancellationToken cancellationToken);

    Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);

    Task<DeletionSchedule> ScheduleOrganizationDeletionAsync(
        OrganizationDeletionRequest request,
        CancellationToken cancellationToken);

    Task CancelOrganizationDeletionAsync(
        Guid actorId,
        Guid organizationId,
        CancellationToken cancellationToken);
}

public interface IInvitationSender
{
    Task SendAsync(IssuedInvitation invitation, CancellationToken cancellationToken);
}

public interface IEmailChangeLifecycle
{
    Task RequestAsync(EmailChangeRequest request, CancellationToken cancellationToken);

    Task<EmailChangeResult> ConfirmAsync(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken);
}

public interface IEmailChangeCodeSender
{
    Task SendAsync(
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}

public interface IIdentityMaintenance
{
    Task<IdentityCleanupResult> CleanupExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken);

    Task<ErasureCandidates> ClaimDueErasuresAsync(
        int batchSize,
        CancellationToken cancellationToken);

    Task CompleteOrganizationErasureAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task CompleteAccountErasureAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IDataErasure
{
    string Area { get; }

    Task<DataErasureResult> EraseOrganizationAsync(
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken);

    Task<DataErasureResult> PseudonymizeUserAsync(
        Guid userId,
        Guid pseudonymousUserId,
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record IdentityCleanupResult(
    int ExpiredLoginChallenges,
    int ExpiredEmailChangeChallenges,
    int ExpiredInvitations,
    int StaleSessions,
    int StaleRateEvents);

public sealed record ErasureCandidates(
    IReadOnlyList<Guid> OrganizationIds,
    IReadOnlyList<Guid> UserIds);

public sealed record DataErasureResult(
    int ChangedRecords,
    int RetryableFailures,
    bool HasRemaining);

public sealed record OrganizationInvitationRequest(
    Guid ActorId,
    string Email,
    string OrganizationName,
    string OrganizationSlug,
    string IpAddress);

public sealed record TeamInvitationRequest(
    Guid ActorId,
    Guid OrganizationId,
    string Email,
    TenantRole Role,
    Guid? CampId,
    string IpAddress);

public sealed record AcceptInvitationRequest(string Token, string DisplayName);

public sealed record IssuedInvitation(
    Guid Id,
    Guid OrganizationId,
    string Token,
    string Email,
    TenantRole Role,
    Guid? CampId,
    DateTimeOffset ExpiresAt);

public sealed record InvitationAcceptance(
    InvitationAcceptanceOutcome Outcome,
    Guid? UserId,
    Guid? OrganizationId,
    bool RequiresLogin);

public sealed record InvitationSummary(
    Guid Id,
    string Email,
    TenantRole Role,
    Guid? CampId,
    DateTimeOffset ExpiresAt,
    bool IsRevoked,
    bool IsUsed);

public sealed record AccountView(
    Guid Id,
    string Email,
    string DisplayName,
    DateTimeOffset? DeletionScheduledAt,
    bool IsPlatformAdmin,
    long Version);

public sealed record AccountMembershipView(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    TenantRole Role);

public sealed record OrganizationDeletionRequest(
    Guid ActorId,
    Guid OrganizationId,
    string ConfirmedSlug,
    DateTimeOffset ReauthenticatedAt);

public sealed record DeletionSchedule(DateTimeOffset ScheduledAt, DateTimeOffset PurgeAt);

public sealed record EmailChangeRequest(Guid UserId, string Email, string IpAddress);

public sealed record ConfirmEmailChangeRequest(Guid UserId, string Email, string Code);

public sealed record EmailChangeResult(EmailChangeOutcome Outcome, string? Email);

public enum TenantRole
{
    Owner,
    OrganizationAdmin,
    CampLead,
    Member,
    Viewer
}

public enum OrganizationStatus
{
    Active,
    Suspended,
    Erasing
}

public enum InvitationAcceptanceOutcome
{
    Accepted,
    Invalid,
    Expired,
    Revoked,
    Used
}

public enum EmailChangeOutcome
{
    Changed,
    Invalid,
    Expired
}

public sealed class IdentityRuleException(string errorCode, string message) : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

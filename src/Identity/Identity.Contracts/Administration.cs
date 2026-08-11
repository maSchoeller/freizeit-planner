namespace Identity.Contracts;

public interface ITransferableInvitationLinks
{
    Task<IssuedInvitationLink> CreateAsync(
        CreateInvitationLinkRequest request,
        CancellationToken cancellationToken);

    Task<InvitationPreview?> PreviewAsync(string token, CancellationToken cancellationToken);

    Task<IssuedInvitationLink> RotateAsync(
        Guid actorId,
        Guid invitationId,
        long expectedVersion,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        Guid actorId,
        Guid invitationId,
        long expectedVersion,
        CancellationToken cancellationToken);
}

public interface IInvitationRegistration
{
    Task<InvitationRegistrationOutcome> BeginAsync(
        InvitationRegistrationRequest request,
        CancellationToken cancellationToken);

    Task<InvitationConfirmationResult> ConfirmAsync(
        InvitationEmailConfirmation request,
        CancellationToken cancellationToken);

    Task<InvitationAcceptanceResult> AcceptExistingAsync(
        ExistingInvitationAcceptance request,
        CancellationToken cancellationToken);
}

public interface IInvitationConfirmationSender
{
    Task SendAsync(
        string email,
        string token,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}

public interface IUserAdministration
{
    Task<AdministrationPage<UserAdministrationView>> SearchUsersAsync(
        UserAdministrationQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SuperAdminOrganizationView>> ListOrganizationsAsync(
        Guid actorId,
        CancellationToken cancellationToken);

    Task<UserAdministrationView> ChangeGlobalAccountStatusAsync(
        ChangeGlobalAccountStatusCommand command,
        CancellationToken cancellationToken);

    Task<UserAdministrationView> ChangeSuperAdminAsync(
        ChangeSuperAdminCommand command,
        CancellationToken cancellationToken);

    Task<UserAdministrationView> ClearLoginLockoutAsync(
        ClearLoginLockoutCommand command,
        CancellationToken cancellationToken);

    Task<OrganizationAdministrationView> ChangeMembershipAsync(
        ChangeMembershipCommand command,
        CancellationToken cancellationToken);

    Task<CampAdministrationView?> ChangeCampAssignmentAsync(
        ChangeCampAssignmentCommand command,
        CancellationToken cancellationToken);
}

public enum MembershipStatus
{
    Active,
    Suspended,
    Removed
}

public enum OrganizationRole
{
    OrganizationAdmin
}

public enum CampRole
{
    CampLead,
    Member,
    Viewer
}

public enum InvitationLinkStatus
{
    Available,
    Reserved,
    Used,
    Revoked,
    Expired
}

public sealed record OrganizationInvitationDraft(string Name, string Slug);

public sealed record InvitationGrant(
    bool IsSuperAdmin,
    Guid? OrganizationId,
    OrganizationRole? OrganizationRole,
    Guid? CampId,
    CampRole? CampRole,
    OrganizationInvitationDraft? NewOrganization = null)
{
    public static InvitationGrant SuperAdmin() => new(true, null, null, null, null);

    public static InvitationGrant ForOrganizationAdmin(Guid organizationId) =>
        new(false, organizationId, global::Identity.Contracts.OrganizationRole.OrganizationAdmin, null, null);

    public static InvitationGrant ForCamp(Guid organizationId, Guid campId, global::Identity.Contracts.CampRole role) =>
        new(false, organizationId, null, campId, role);

    public static InvitationGrant ForNewOrganization(string name, string slug) =>
        new(false, null, global::Identity.Contracts.OrganizationRole.OrganizationAdmin, null, null,
            new OrganizationInvitationDraft(name, slug));
}

public sealed record CreateInvitationLinkRequest(
    Guid ActorId,
    InvitationGrant Grant,
    string IpAddress);

public sealed record IssuedInvitationLink(
    Guid Id,
    string Token,
    InvitationGrant Grant,
    DateTimeOffset ExpiresAt,
    long Version);

public sealed record InvitationPreview(
    InvitationGrant Grant,
    string? OrganizationName,
    string? CampName,
    DateTimeOffset ExpiresAt,
    InvitationLinkStatus Status);

public sealed record InvitationRegistrationRequest(
    string InvitationToken,
    string Email,
    string Password,
    string PasswordConfirmation,
    string FirstName,
    string LastName,
    string IpAddress);

public sealed record InvitationEmailConfirmation(string Token, string IpAddress);

public sealed record ExistingInvitationAcceptance(
    string InvitationToken,
    Guid UserId);

public sealed record InvitationConfirmationResult(
    InvitationConfirmationOutcome Outcome,
    InvitationGrant? Grant,
    IssuedAuthentication? Authentication)
{
    public static InvitationConfirmationResult Succeeded(
        InvitationGrant grant,
        IssuedAuthentication authentication) =>
        new(InvitationConfirmationOutcome.Succeeded, grant, authentication);

    public static InvitationConfirmationResult Failed(InvitationConfirmationOutcome outcome) =>
        new(outcome, null, null);
}

public sealed record InvitationAcceptanceResult(
    InvitationAcceptanceOutcome Outcome,
    InvitationGrant? Grant)
{
    public static InvitationAcceptanceResult Succeeded(InvitationGrant grant) =>
        new(InvitationAcceptanceOutcome.Accepted, grant);

    public static InvitationAcceptanceResult Failed(InvitationAcceptanceOutcome outcome) =>
        new(outcome, null);
}

public enum InvitationRegistrationOutcome
{
    ConfirmationRequired,
    InvalidInvitation,
    Reserved,
    ExistingAccount,
    InvalidInput
}

public enum InvitationConfirmationOutcome
{
    Succeeded,
    Invalid,
    Expired,
    Used,
    Revoked
}

public sealed record OrganizationAdministrationView(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    MembershipStatus Status,
    OrganizationRole? Role,
    IReadOnlyList<CampAdministrationView> Camps,
    long Version);

public sealed record CampAdministrationView(
    Guid CampId,
    string CampName,
    CampRole Role,
    long Version);

public sealed record UserAdministrationView(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    AccountStatus AccountStatus,
    bool IsSuperAdmin,
    DateTimeOffset? LoginLockedUntil,
    IReadOnlyList<OrganizationAdministrationView> Organizations,
    long Version);

public sealed record UserAdministrationQuery(
    Guid ActorId,
    string? Search,
    int Page = 1,
    int PageSize = 25,
    Guid? OrganizationId = null);

public sealed record AdministrationPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ChangeGlobalAccountStatusCommand(
    Guid ActorId,
    Guid UserId,
    AccountStatus Status,
    long ExpectedVersion);

public sealed record ChangeSuperAdminCommand(
    Guid ActorId,
    Guid UserId,
    bool IsSuperAdmin,
    long ExpectedVersion);

public sealed record ClearLoginLockoutCommand(
    Guid ActorId,
    Guid UserId,
    long ExpectedVersion);

public sealed record ChangeMembershipCommand(
    Guid ActorId,
    Guid OrganizationId,
    Guid UserId,
    MembershipStatus Status,
    OrganizationRole? Role,
    long ExpectedVersion);

public sealed record ChangeCampAssignmentCommand(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid UserId,
    CampRole? Role,
    long ExpectedVersion);

using System.Security.Cryptography;
using System.Text;
using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class TransferableInvitationLinkService(
    IdentityDbContext dbContext,
    TimeProvider timeProvider,
    byte[] tokenPepper) : ITransferableInvitationLinks
{
    private static readonly TimeSpan SuperAdminLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan OrganizationAdminLifetime = TimeSpan.FromHours(48);
    private static readonly TimeSpan CampLifetime = TimeSpan.FromDays(7);
    private readonly byte[] tokenPepper = tokenPepper.Length >= 32
        ? tokenPepper.ToArray()
        : throw new ArgumentException("The invitation-token pepper must contain at least 32 bytes.", nameof(tokenPepper));

    public async Task<IssuedInvitationLink> CreateAsync(
        CreateInvitationLinkRequest request,
        CancellationToken cancellationToken)
    {
        ValidateGrant(request.Grant);
        await AuthorizeAsync(request.ActorId, request.Grant, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var entity = ToEntity(request.ActorId, request.Grant, token, now, null);
        dbContext.TransferableInvitations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToIssued(entity, token);
    }

    public async Task<InvitationPreview?> PreviewAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsToken(token)) return null;
        var tokenHash = HashToken(token);
        var invitation = await dbContext.TransferableInvitations.SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash,
            cancellationToken);
        if (invitation is null) return null;
        var organizationName = invitation.OrganizationId is { } organizationId
            ? await dbContext.Organizations.Where(item => item.Id == organizationId)
                .Select(item => item.Name).SingleOrDefaultAsync(cancellationToken)
            : invitation.NewOrganizationName;
        return new InvitationPreview(
            ToGrant(invitation),
            organizationName,
            null,
            invitation.ExpiresAt,
            Status(invitation, timeProvider.GetUtcNow()));
    }

    public async Task<IssuedInvitationLink> RotateAsync(
        Guid actorId,
        Guid invitationId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var invitation = await RequireInvitationAsync(invitationId, cancellationToken);
        var grant = ToGrant(invitation);
        await AuthorizeAsync(actorId, grant, cancellationToken);
        EnsureVersion(invitation, expectedVersion);
        if (invitation.UsedAt is not null)
            throw Rule("invitation_used", "Eine verwendete Einladung kann nicht rotiert werden.");
        var now = timeProvider.GetUtcNow();
        invitation.RevokedAt ??= now;
        invitation.Version++;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var replacement = ToEntity(actorId, grant, token, now, invitation.Id);
        dbContext.TransferableInvitations.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToIssued(replacement, token);
    }

    public async Task RevokeAsync(
        Guid actorId,
        Guid invitationId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var invitation = await RequireInvitationAsync(invitationId, cancellationToken);
        await AuthorizeAsync(actorId, ToGrant(invitation), cancellationToken);
        EnsureVersion(invitation, expectedVersion);
        invitation.RevokedAt ??= timeProvider.GetUtcNow();
        invitation.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AuthorizeAsync(Guid actorId, InvitationGrant grant, CancellationToken cancellationToken)
    {
        var actor = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == actorId && item.DeletionScheduledAt == null,
            cancellationToken);
        if (actor is null || actor.AccountStatus == AccountStatus.Suspended)
            throw Rule("actor_not_active", "Das handelnde Konto ist nicht aktiv.");
        if (actor.IsSuperAdmin) return;
        if (grant.IsSuperAdmin || grant.NewOrganization is not null || grant.OrganizationId is not { } organizationId)
            throw Rule("superadmin_required", "Diesen Einladungslink darf nur ein SuperAdmin erstellen.");
        var canManage = await dbContext.Memberships.AnyAsync(
            item => item.OrganizationId == organizationId
                && item.UserId == actorId
                && item.IsActive
                && (item.Role == TenantRole.Owner || item.Role == TenantRole.OrganizationAdmin),
            cancellationToken);
        if (!canManage)
            throw Rule("organization_admin_required", "Diesen Einladungslink darf nur ein Orgadmin erstellen.");
    }

    private TransferableInvitationEntity ToEntity(
        Guid actorId,
        InvitationGrant grant,
        string token,
        DateTimeOffset now,
        Guid? rotatedFromId) => new()
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = actorId,
            TokenHash = HashToken(token),
            IsSuperAdmin = grant.IsSuperAdmin,
            OrganizationId = grant.OrganizationId,
            OrganizationRole = grant.OrganizationRole,
            CampId = grant.CampId,
            CampRole = grant.CampRole,
            NewOrganizationName = grant.NewOrganization?.Name,
            NewOrganizationSlug = grant.NewOrganization?.Slug,
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime(grant)),
            RotatedFromId = rotatedFromId
        };

    private string HashToken(string token) => Convert.ToHexString(
        HMACSHA256.HashData(tokenPepper, Encoding.UTF8.GetBytes(token)));

    private static bool IsToken(string token) =>
        token is { Length: 64 } && token.All(Uri.IsHexDigit);

    private static TimeSpan Lifetime(InvitationGrant grant) => grant.IsSuperAdmin
        ? SuperAdminLifetime
        : grant.CampRole is not null
            ? CampLifetime
            : OrganizationAdminLifetime;

    private static InvitationGrant ToGrant(TransferableInvitationEntity entity) => new(
        entity.IsSuperAdmin,
        entity.OrganizationId,
        entity.OrganizationRole,
        entity.CampId,
        entity.CampRole,
        entity.NewOrganizationName is not null && entity.NewOrganizationSlug is not null
            ? new OrganizationInvitationDraft(entity.NewOrganizationName, entity.NewOrganizationSlug)
            : null);

    private static IssuedInvitationLink ToIssued(TransferableInvitationEntity entity, string token) =>
        new(entity.Id, token, ToGrant(entity), entity.ExpiresAt, entity.Version);

    private static InvitationLinkStatus Status(TransferableInvitationEntity invitation, DateTimeOffset now)
    {
        if (invitation.RevokedAt is not null) return InvitationLinkStatus.Revoked;
        if (invitation.UsedAt is not null) return InvitationLinkStatus.Used;
        if (invitation.ExpiresAt <= now) return InvitationLinkStatus.Expired;
        if (invitation.ReservedUntil > now) return InvitationLinkStatus.Reserved;
        return InvitationLinkStatus.Available;
    }

    private static void ValidateGrant(InvitationGrant grant)
    {
        var isGlobal = grant.IsSuperAdmin && grant.OrganizationId is null && grant.OrganizationRole is null
            && grant.CampId is null && grant.CampRole is null && grant.NewOrganization is null;
        var isOrganization = !grant.IsSuperAdmin && grant.OrganizationRole == OrganizationRole.OrganizationAdmin
            && grant.CampId is null && grant.CampRole is null
            && ((grant.OrganizationId is not null) != (grant.NewOrganization is not null));
        var isCamp = !grant.IsSuperAdmin && grant.OrganizationId is not null && grant.OrganizationRole is null
            && grant.CampId is not null && grant.CampRole is not null && grant.NewOrganization is null;
        if (!isGlobal && !isOrganization && !isCamp)
            throw Rule("invalid_invitation_grant", "Die Rollenfreigabe der Einladung ist ungültig.");
    }

    private async Task<TransferableInvitationEntity> RequireInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken) =>
        await dbContext.TransferableInvitations.SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken)
        ?? throw Rule("invitation_not_found", "Die Einladung wurde nicht gefunden.");

    private static void EnsureVersion(TransferableInvitationEntity invitation, long expectedVersion)
    {
        if (invitation.Version != expectedVersion)
            throw Rule("version_conflict", "Die Einladung wurde zwischenzeitlich geändert.");
    }

    private static IdentityRuleException Rule(string code, string message) => new(code, message);
}

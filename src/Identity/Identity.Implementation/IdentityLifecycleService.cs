using System.Security.Cryptography;
using System.Text;
using Identity.Contracts;

namespace Identity.Implementation;

public sealed class IdentityLifecycleService(
    IIdentityLifecycleState state,
    TimeProvider timeProvider,
    byte[] tokenPepper) : IInvitationLifecycle, IAccountLifecycle
{
    private static readonly TimeSpan PlatformInvitationLifetime = TimeSpan.FromHours(48);
    private static readonly TimeSpan TeamInvitationLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan DeletionGracePeriod = TimeSpan.FromDays(30);
    private static readonly TimeSpan FreshReauthenticationWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InvitationRateWindow = TimeSpan.FromMinutes(15);
    private const int MaxInvitationsPerWindow = 5;
    private readonly byte[] tokenPepper = tokenPepper.Length >= 32
        ? tokenPepper.ToArray()
        : throw new ArgumentException("The invitation-token pepper must contain at least 32 bytes.", nameof(tokenPepper));

    public async Task<IReadOnlyList<InvitationSummary>> ListInvitationsAsync(
        Guid actorId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _ = await RequireActiveOrganizationAsync(organizationId, cancellationToken);
        var membership = await RequireActiveMembershipAsync(organizationId, actorId, cancellationToken);
        if (membership.Role is not (TenantRole.Owner or TenantRole.OrganizationAdmin))
        {
            throw Rule("role_escalation", "Diese Rolle darf Einladungen nicht verwalten.");
        }

        return (await state.ListInvitationsAsync(organizationId, cancellationToken))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new InvitationSummary(
                item.Id,
                item.NormalizedEmail,
                item.Role,
                item.CampId,
                item.ExpiresAt,
                item.RevokedAt is not null,
                item.UsedAt is not null))
            .ToArray();
    }

    public async Task<IssuedInvitation> CreateOrganizationInvitationAsync(
        OrganizationInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireUserAsync(request.ActorId, cancellationToken);
        if (!actor.IsPlatformAdmin)
        {
            throw Rule("platform_admin_required", "Nur ein Platform Admin darf eine Organization anlegen.");
        }

        var slug = NormalizeSlug(request.OrganizationSlug);
        if (await state.FindOrganizationBySlugAsync(slug, cancellationToken) is not null)
        {
            throw Rule("organization_slug_conflict", "Dieser Organization-Slug ist bereits vergeben.");
        }

        var now = timeProvider.GetUtcNow();
        await EnforceInvitationRateAsync(request.Email, request.IpAddress, now, cancellationToken);
        var organization = new OrganizationRecord(Guid.NewGuid(), RequireText(request.OrganizationName, 160), slug);
        var issued = CreateInvitation(
            organization.Id,
            request.Email,
            TenantRole.Owner,
            null,
            now,
            PlatformInvitationLifetime,
            true,
            null);
        await state.SaveOrganizationInvitationAsync(organization, issued.Record, cancellationToken);
        return issued.View;
    }

    public async Task<IssuedInvitation> IssueTeamInvitationAsync(
        TeamInvitationRequest request,
        CancellationToken cancellationToken)
    {
        _ = await RequireActiveOrganizationAsync(request.OrganizationId, cancellationToken);
        var actorMembership = await RequireActiveMembershipAsync(
            request.OrganizationId,
            request.ActorId,
            cancellationToken);
        EnsureCanInvite(actorMembership.Role, request.Role);
        ValidateRoleScope(request.Role, request.CampId);

        var now = timeProvider.GetUtcNow();
        await EnforceInvitationRateAsync(request.Email, request.IpAddress, now, cancellationToken);
        var issued = CreateInvitation(
            request.OrganizationId,
            request.Email,
            request.Role,
            request.CampId,
            now,
            TeamInvitationLifetime,
            false,
            null);
        await state.SaveInvitationAsync(issued.Record, cancellationToken);
        return issued.View;
    }

    public async Task<IssuedInvitation> RotateInvitationAsync(
        Guid actorId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var invitation = await state.FindInvitationAsync(invitationId, cancellationToken)
            ?? throw Rule("invitation_not_found", "Die Einladung wurde nicht gefunden.");
        await EnsureCanManageInvitationAsync(actorId, invitation, cancellationToken);
        if (invitation.UsedAt is not null)
        {
            throw Rule("invitation_used", "Eine bereits angenommene Einladung kann nicht rotiert werden.");
        }

        var now = timeProvider.GetUtcNow();
        invitation.Revoke(now);
        await state.SaveInvitationAsync(invitation, cancellationToken);
        var issued = CreateInvitation(
            invitation.OrganizationId,
            invitation.NormalizedEmail,
            invitation.Role,
            invitation.CampId,
            now,
            invitation.IsPlatformInvitation ? PlatformInvitationLifetime : TeamInvitationLifetime,
            invitation.IsPlatformInvitation,
            invitation.Id);
        await state.SaveInvitationAsync(issued.Record, cancellationToken);
        return issued.View;
    }

    public async Task RevokeInvitationAsync(
        Guid actorId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var invitation = await state.FindInvitationAsync(invitationId, cancellationToken)
            ?? throw Rule("invitation_not_found", "Die Einladung wurde nicht gefunden.");
        await EnsureCanManageInvitationAsync(actorId, invitation, cancellationToken);
        invitation.Revoke(timeProvider.GetUtcNow());
        await state.SaveInvitationAsync(invitation, cancellationToken);
    }

    public async Task<InvitationAcceptance> AcceptInvitationAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadInvitationId(request.Token, out var invitationId))
        {
            return new InvitationAcceptance(InvitationAcceptanceOutcome.Invalid, null, null, false);
        }

        var invitation = await state.FindInvitationAsync(invitationId, cancellationToken);
        if (invitation is null || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(invitation.TokenHash),
                Convert.FromHexString(HashToken(request.Token))))
        {
            return new InvitationAcceptance(InvitationAcceptanceOutcome.Invalid, null, null, false);
        }

        if (invitation.RevokedAt is not null)
        {
            return new InvitationAcceptance(InvitationAcceptanceOutcome.Revoked, null, null, false);
        }
        if (invitation.UsedAt is not null)
        {
            return new InvitationAcceptance(InvitationAcceptanceOutcome.Used, null, null, false);
        }

        var now = timeProvider.GetUtcNow();
        if (invitation.ExpiresAt <= now)
        {
            return new InvitationAcceptance(InvitationAcceptanceOutcome.Expired, null, null, false);
        }

        var user = await state.FindUserByEmailAsync(invitation.NormalizedEmail, cancellationToken);
        var isNewUser = user is null;
        user ??= new LifecycleUser(
            Guid.NewGuid(),
            invitation.NormalizedEmail.ToLowerInvariant(),
            invitation.NormalizedEmail,
            RequireText(request.DisplayName, 160));
        invitation.MarkUsed(now);
        var organizationRole = invitation.Role is TenantRole.Owner or TenantRole.OrganizationAdmin
            ? invitation.Role
            : TenantRole.Viewer;
        var existingMembership = await state.FindMembershipAsync(
            invitation.OrganizationId,
            user.Id,
            cancellationToken);
        if (existingMembership is { IsActive: true }
            && existingMembership.Role < organizationRole)
        {
            organizationRole = existingMembership.Role;
        }
        var membership = new MembershipRecord(invitation.OrganizationId, user.Id, organizationRole);
        var assignment = invitation.CampId is { } campId
            ? new CampAssignmentRecord(invitation.OrganizationId, campId, user.Id, invitation.Role)
            : null;
        if (!await state.TryAcceptInvitationAsync(
                invitation,
                user,
                membership,
                assignment,
                cancellationToken))
        {
            return new InvitationAcceptance(InvitationAcceptanceOutcome.Used, null, null, false);
        }
        return new InvitationAcceptance(
            InvitationAcceptanceOutcome.Accepted,
            user.Id,
            invitation.OrganizationId,
            isNewUser);
    }

    public async Task<AccountView> UpdateDisplayNameAsync(
        Guid userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        user.Rename(RequireText(displayName, 160));
        await state.SaveUserAsync(user, cancellationToken);
        return new AccountView(user.Id, user.Email, user.DisplayName, user.DeletionScheduledAt);
    }

    public async Task<AccountView> GetAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        return new AccountView(user.Id, user.Email, user.DisplayName, user.DeletionScheduledAt);
    }

    public async Task<IReadOnlyList<AccountMembershipView>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = new List<AccountMembershipView>();
        foreach (var membership in await state.ListMembershipsAsync(userId, cancellationToken))
        {
            if (!membership.IsActive)
            {
                continue;
            }
            var organization = await state.FindOrganizationAsync(membership.OrganizationId, cancellationToken);
            if (organization is not null)
            {
                result.Add(new AccountMembershipView(
                    organization.Id,
                    organization.Name,
                    organization.Slug,
                    membership.Role));
            }
        }
        return result;
    }

    public async Task<DeletionSchedule> ScheduleAccountDeletionAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        foreach (var membership in await state.ListMembershipsAsync(userId, cancellationToken))
        {
            if (membership.IsActive
                && membership.Role == TenantRole.Owner
                && await state.CountActiveOwnersAsync(membership.OrganizationId, cancellationToken) <= 1)
            {
                throw Rule("last_owner", "Das Konto ist der letzte aktive Owner einer Organization.");
            }
        }

        var now = timeProvider.GetUtcNow();
        user.ScheduleDeletion(now);
        await state.SaveUserAsync(user, cancellationToken);
        return new DeletionSchedule(now, now.Add(DeletionGracePeriod));
    }

    public async Task CancelAccountDeletionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        user.CancelDeletion();
        await state.SaveUserAsync(user, cancellationToken);
    }

    public async Task LeaveOrganizationAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var membership = await RequireActiveMembershipAsync(organizationId, userId, cancellationToken);
        if (membership.Role == TenantRole.Owner
            && await state.CountActiveOwnersAsync(organizationId, cancellationToken) <= 1)
        {
            throw Rule("last_owner", "Der letzte aktive Owner kann die Organization nicht verlassen.");
        }

        membership.Leave();
        await state.SaveMembershipAsync(membership, cancellationToken);
    }

    public async Task<DeletionSchedule> ScheduleOrganizationDeletionAsync(
        OrganizationDeletionRequest request,
        CancellationToken cancellationToken)
    {
        var organization = await RequireActiveOrganizationAsync(request.OrganizationId, cancellationToken);
        var membership = await RequireActiveMembershipAsync(
            request.OrganizationId,
            request.ActorId,
            cancellationToken);
        if (membership.Role != TenantRole.Owner)
        {
            throw Rule("owner_required", "Nur ein Organization Owner darf die Löschung vormerken.");
        }
        if (!string.Equals(organization.Slug, request.ConfirmedSlug.Trim(), StringComparison.Ordinal))
        {
            throw Rule("slug_confirmation_invalid", "Der eingegebene Organization-Slug stimmt nicht überein.");
        }

        var now = timeProvider.GetUtcNow();
        if (request.ReauthenticatedAt > now
            || now - request.ReauthenticatedAt > FreshReauthenticationWindow)
        {
            throw Rule("fresh_reauthentication_required", "Bitte bestätige die Löschung mit einem frischen Anmeldecode.");
        }

        organization.ScheduleDeletion(now);
        await state.SaveOrganizationAsync(organization, cancellationToken);
        return new DeletionSchedule(now, now.Add(DeletionGracePeriod));
    }

    public async Task CancelOrganizationDeletionAsync(
        Guid actorId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await state.FindOrganizationAsync(organizationId, cancellationToken)
            ?? throw Rule("organization_not_found", "Die Organization wurde nicht gefunden.");
        var membership = await RequireActiveMembershipAsync(organizationId, actorId, cancellationToken);
        if (membership.Role != TenantRole.Owner)
        {
            throw Rule("owner_required", "Nur ein Organization Owner darf die Löschung abbrechen.");
        }

        organization.CancelDeletion();
        await state.SaveOrganizationAsync(organization, cancellationToken);
    }

    private async ValueTask<LifecycleUser> RequireUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await state.FindUserAsync(userId, cancellationToken)
            ?? throw Rule("user_not_found", "Das Konto wurde nicht gefunden.");
    }

    private async ValueTask<OrganizationRecord> RequireActiveOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await state.FindOrganizationAsync(organizationId, cancellationToken)
            ?? throw Rule("organization_not_found", "Die Organization wurde nicht gefunden.");
        if (organization.Status == OrganizationStatus.Suspended)
        {
            throw Rule("organization_suspended", "Die Organization ist gesperrt.");
        }
        return organization;
    }

    private async ValueTask<MembershipRecord> RequireActiveMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await state.FindMembershipAsync(organizationId, userId, cancellationToken);
        return membership is { IsActive: true }
            ? membership
            : throw Rule("membership_required", "Für diese Organization besteht keine aktive Mitgliedschaft.");
    }

    private async Task EnsureCanManageInvitationAsync(
        Guid actorId,
        InvitationRecord invitation,
        CancellationToken cancellationToken)
    {
        if (invitation.IsPlatformInvitation)
        {
            var actor = await RequireUserAsync(actorId, cancellationToken);
            if (!actor.IsPlatformAdmin)
            {
                throw Rule("platform_admin_required", "Nur ein Platform Admin darf diese Einladung verwalten.");
            }
            return;
        }

        var membership = await RequireActiveMembershipAsync(invitation.OrganizationId, actorId, cancellationToken);
        EnsureCanInvite(membership.Role, invitation.Role);
    }

    private static void EnsureCanInvite(TenantRole actorRole, TenantRole targetRole)
    {
        var allowed = actorRole == TenantRole.Owner
            || (actorRole == TenantRole.OrganizationAdmin
                && targetRole is TenantRole.CampLead or TenantRole.Member or TenantRole.Viewer);
        if (!allowed)
        {
            throw Rule("role_escalation", "Diese Rolle darf nicht vergeben werden.");
        }
    }

    private static void ValidateRoleScope(TenantRole role, Guid? campId)
    {
        var isCampRole = role is TenantRole.CampLead or TenantRole.Member or TenantRole.Viewer;
        if (isCampRole != campId.HasValue)
        {
            throw Rule("role_scope_invalid", "Camp-Rollen benötigen genau ein Camp; Organization-Rollen dürfen keines enthalten.");
        }
    }

    private (InvitationRecord Record, IssuedInvitation View) CreateInvitation(
        Guid organizationId,
        string email,
        TenantRole role,
        Guid? campId,
        DateTimeOffset now,
        TimeSpan lifetime,
        bool isPlatformInvitation,
        Guid? rotatedFromId)
    {
        var id = Guid.NewGuid();
        var normalizedEmail = NormalizeEmail(email);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var token = $"{id:N}.{secret}";
        var expiresAt = now.Add(lifetime);
        var record = new InvitationRecord(
            id,
            organizationId,
            normalizedEmail,
            role,
            campId,
            HashToken(token),
            now,
            expiresAt,
            isPlatformInvitation,
            rotatedFromId: rotatedFromId);
        var view = new IssuedInvitation(id, organizationId, token, normalizedEmail, role, campId, expiresAt);
        return (record, view);
    }

    private string HashToken(string token)
    {
        return Convert.ToHexString(HMACSHA256.HashData(tokenPepper, Encoding.UTF8.GetBytes(token)));
    }

    private async Task EnforceInvitationRateAsync(
        string email,
        string ipAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var partitions = new[]
        {
            CreateRatePartition("invitation:email", NormalizeEmail(email)),
            CreateRatePartition("invitation:ip", ipAddress)
        };
        foreach (var partition in partitions)
        {
            if (await state.CountInvitationRateEventsAsync(
                    partition,
                    now.Subtract(InvitationRateWindow),
                    cancellationToken) >= MaxInvitationsPerWindow)
            {
                throw Rule("invitation_rate_limited", "Zu viele Einladungen. Bitte warte einige Minuten.");
            }
        }
        foreach (var partition in partitions)
        {
            await state.AddInvitationRateEventAsync(new RateEvent(partition, now), cancellationToken);
        }
    }

    private string CreateRatePartition(string purpose, string value)
    {
        var digest = HMACSHA256.HashData(
            tokenPepper,
            Encoding.UTF8.GetBytes($"{purpose}|{value}"));
        return $"{purpose}:{Convert.ToHexString(digest)}";
    }

    private static bool TryReadInvitationId(string token, out Guid invitationId)
    {
        invitationId = Guid.Empty;
        var separator = token.IndexOf('.', StringComparison.Ordinal);
        return separator == 32 && Guid.TryParseExact(token[..separator], "N", out invitationId);
    }

    private static string NormalizeEmail(string email)
    {
        var value = RequireText(email, 320);
        if (!value.Contains('@', StringComparison.Ordinal))
        {
            throw Rule("email_invalid", "Die E-Mail-Adresse ist ungültig.");
        }
        return value.ToUpperInvariant();
    }

    private static string NormalizeSlug(string slug)
    {
        var value = RequireText(slug, 80).ToLowerInvariant();
        if (value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '-'))
            || value.StartsWith('-')
            || value.EndsWith('-'))
        {
            throw Rule("slug_invalid", "Der Slug darf nur Kleinbuchstaben, Ziffern und Bindestriche enthalten.");
        }
        return value;
    }

    private static string RequireText(string value, int maxLength)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is 0 || trimmed.Length > maxLength)
        {
            throw Rule("validation_failed", "Eine erforderliche Eingabe fehlt oder ist zu lang.");
        }
        return trimmed;
    }

    private static IdentityRuleException Rule(string code, string message) => new(code, message);
}

using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class InvitationRegistrationService(
    IdentityDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IInvitationConfirmationSender sender,
    IAuthenticationTokenIssuer tokenIssuer,
    TimeProvider timeProvider,
    byte[] invitationTokenPepper,
    byte[] sessionTokenPepper) : IInvitationRegistration
{
    private static readonly TimeSpan ReservationLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RememberedSessionLifetime = TimeSpan.FromDays(30);
    private readonly byte[] invitationTokenPepper = RequirePepper(invitationTokenPepper, "invitation");
    private readonly byte[] sessionTokenPepper = RequirePepper(sessionTokenPepper, "session");

    public async Task<InvitationRegistrationOutcome> BeginAsync(
        InvitationRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (!IsValidIdentity(email, firstName, lastName, request.Password, request.PasswordConfirmation))
            return InvitationRegistrationOutcome.InvalidInput;

        var now = timeProvider.GetUtcNow();
        var invitation = await FindInvitationAsync(request.InvitationToken, cancellationToken);
        if (invitation is null || invitation.RevokedAt is not null || invitation.UsedAt is not null
            || invitation.ExpiresAt <= now)
            return InvitationRegistrationOutcome.InvalidInvitation;

        var existingUser = await dbContext.Users.SingleOrDefaultAsync(
            item => item.NormalizedEmail == normalizedEmail && item.DeletionScheduledAt == null,
            cancellationToken);
        if (existingUser is { EmailConfirmed: true })
            return InvitationRegistrationOutcome.ExistingAccount;
        if (invitation.ReservedUntil > now && invitation.ReservedByUserId != existingUser?.Id)
            return InvitationRegistrationOutcome.Reserved;

        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var user = existingUser;
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                NormalizedUserName = normalizedEmail,
                Email = email,
                NormalizedEmail = normalizedEmail,
                EmailConfirmed = false,
                FirstName = firstName,
                LastName = lastName,
                DisplayName = $"{firstName} {lastName}",
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            dbContext.Users.Add(user);
        }
        else
        {
            user.UserName = email;
            user.NormalizedUserName = normalizedEmail;
            user.Email = email;
            user.FirstName = firstName;
            user.LastName = lastName;
            user.DisplayName = $"{firstName} {lastName}";
        }
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        var previous = await dbContext.InvitationRegistrations
            .Where(item => item.InvitationId == invitation.Id && item.UserId == user.Id && item.UsedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var registration in previous) registration.UsedAt = now;
        var confirmationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.Add(ReservationLifetime);
        dbContext.InvitationRegistrations.Add(new InvitationRegistrationEntity
        {
            Id = Guid.NewGuid(),
            InvitationId = invitation.Id,
            UserId = user.Id,
            TokenHash = HashInvitationToken(confirmationToken),
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
        invitation.ReservedByUserId = user.Id;
        invitation.ReservedUntil = expiresAt;
        invitation.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await sender.SendAsync(email, confirmationToken, expiresAt, cancellationToken);
        return InvitationRegistrationOutcome.ConfirmationRequired;
    }

    public async Task<InvitationConfirmationResult> ConfirmAsync(
        InvitationEmailConfirmation request,
        CancellationToken cancellationToken)
    {
        if (!IsToken(request.Token))
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Invalid);
        var registration = await dbContext.InvitationRegistrations.SingleOrDefaultAsync(
            item => item.TokenHash == HashInvitationToken(request.Token),
            cancellationToken);
        if (registration is null)
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Invalid);
        if (registration.UsedAt is not null)
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Used);
        var now = timeProvider.GetUtcNow();
        if (registration.ExpiresAt <= now)
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Expired);
        var invitation = await dbContext.TransferableInvitations.SingleOrDefaultAsync(
            item => item.Id == registration.InvitationId,
            cancellationToken);
        if (invitation is null)
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Invalid);
        if (invitation.RevokedAt is not null)
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Revoked);
        if (invitation.UsedAt is not null)
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Used);
        if (invitation.ExpiresAt <= now || invitation.ReservedUntil <= now
            || invitation.ReservedByUserId != registration.UserId)
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Expired);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == registration.UserId && item.DeletionScheduledAt == null,
            cancellationToken);
        if (user is null || user.EmailConfirmed)
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Invalid);

        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var grant = await ApplyGrantAsync(invitation, user, cancellationToken);
        user.EmailConfirmed = true;
        user.Version++;
        registration.UsedAt = now;
        invitation.UsedAt = now;
        invitation.ReservedByUserId = null;
        invitation.ReservedUntil = null;
        invitation.Version++;
        var authentication = IssueAuthentication(user, request.IpAddress, now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return InvitationConfirmationResult.Failed(InvitationConfirmationOutcome.Used);
        }
        return InvitationConfirmationResult.Succeeded(grant, authentication);
    }

    public async Task<InvitationAcceptanceResult> AcceptExistingAsync(
        ExistingInvitationAcceptance request,
        CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(request.InvitationToken, cancellationToken);
        if (invitation is null) return InvitationAcceptanceResult.Failed(InvitationAcceptanceOutcome.Invalid);
        var now = timeProvider.GetUtcNow();
        if (invitation.RevokedAt is not null)
            return InvitationAcceptanceResult.Failed(InvitationAcceptanceOutcome.Revoked);
        if (invitation.UsedAt is not null)
            return InvitationAcceptanceResult.Failed(InvitationAcceptanceOutcome.Used);
        if (invitation.ExpiresAt <= now)
            return InvitationAcceptanceResult.Failed(InvitationAcceptanceOutcome.Expired);
        if (invitation.ReservedUntil > now)
            return InvitationAcceptanceResult.Failed(InvitationAcceptanceOutcome.Reserved);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == request.UserId && item.EmailConfirmed && item.DeletionScheduledAt == null,
            cancellationToken);
        if (user is null || user.AccountStatus == AccountStatus.Suspended)
            return InvitationAcceptanceResult.Failed(InvitationAcceptanceOutcome.Invalid);

        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var grant = await ApplyGrantAsync(invitation, user, cancellationToken);
        invitation.UsedAt = now;
        invitation.Version++;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return InvitationAcceptanceResult.Failed(InvitationAcceptanceOutcome.Used);
        }
        return InvitationAcceptanceResult.Succeeded(grant);
    }

    private async Task<InvitationGrant> ApplyGrantAsync(
        TransferableInvitationEntity invitation,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (invitation.IsSuperAdmin)
        {
            user.IsSuperAdmin = true;
            return ToGrant(invitation);
        }

        var organizationId = invitation.OrganizationId;
        if (invitation.NewOrganizationName is not null && invitation.NewOrganizationSlug is not null)
        {
            organizationId = Guid.NewGuid();
            dbContext.Organizations.Add(new OrganizationEntity
            {
                Id = organizationId.Value,
                Name = invitation.NewOrganizationName,
                Slug = invitation.NewOrganizationSlug,
                Status = OrganizationStatus.Active
            });
            invitation.OrganizationId = organizationId;
        }
        if (organizationId is null || !await OrganizationExistsAsync(organizationId.Value, cancellationToken))
            throw new IdentityRuleException("organization_not_found", "Die Organization wurde nicht gefunden.");

        var membership = await dbContext.Memberships.SingleOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.UserId == user.Id,
            cancellationToken);
        var organizationRole = invitation.OrganizationRole == OrganizationRole.OrganizationAdmin;
        if (membership is null)
        {
            membership = new MembershipEntity
            {
                OrganizationId = organizationId.Value,
                UserId = user.Id,
                Role = organizationRole ? TenantRole.OrganizationAdmin : TenantRole.Viewer,
                Status = MembershipStatus.Active,
                OrganizationRole = organizationRole ? OrganizationRole.OrganizationAdmin : null
            };
            dbContext.Memberships.Add(membership);
        }
        else
        {
            membership.IsActive = true;
            membership.Status = MembershipStatus.Active;
            if (organizationRole)
            {
                membership.Role = TenantRole.OrganizationAdmin;
                membership.OrganizationRole = OrganizationRole.OrganizationAdmin;
            }
            membership.Version++;
        }

        if (invitation.CampId is { } campId && invitation.CampRole is { } campRole)
        {
            var assignment = await dbContext.CampAssignments.SingleOrDefaultAsync(
                item => item.CampId == campId && item.UserId == user.Id,
                cancellationToken);
            if (assignment is null)
            {
                dbContext.CampAssignments.Add(new CampAssignmentEntity
                {
                    OrganizationId = organizationId.Value,
                    CampId = campId,
                    UserId = user.Id,
                    Role = ToTenantRole(campRole),
                    CampRole = campRole
                });
            }
            else
            {
                assignment.OrganizationId = organizationId.Value;
                assignment.Role = ToTenantRole(campRole);
                assignment.CampRole = campRole;
                assignment.IsActive = true;
                assignment.Version++;
            }
        }
        return ToGrant(invitation);
    }

    private async Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (dbContext.Organizations.Local.Any(item => item.Id == organizationId)) return true;
        return await dbContext.Organizations.AnyAsync(item => item.Id == organizationId, cancellationToken);
    }

    private IssuedAuthentication IssueAuthentication(ApplicationUser user, string ipAddress, DateTimeOffset now)
    {
        var sessionId = Guid.NewGuid();
        var refreshExpiresAt = now.Add(RememberedSessionLifetime);
        var pair = tokenIssuer.Issue(new AuthenticationTokenRequest(
            user.Id,
            sessionId,
            user.DisplayName,
            user.SecurityStamp ?? string.Empty,
            now,
            refreshExpiresAt));
        dbContext.LoginSessions.Add(new LoginSessionEntity
        {
            Id = sessionId,
            UserId = user.Id,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
            IpAddress = ipAddress,
            RefreshTokenHash = HashSessionToken(pair.RefreshToken),
            RememberMe = true,
            ReauthenticatedAt = now
        });
        return new IssuedAuthentication(
            sessionId,
            new AccessTokenResponse(pair.AccessToken, pair.AccessExpiresAt),
            pair.RefreshToken,
            refreshExpiresAt,
            true);
    }

    private async Task<TransferableInvitationEntity?> FindInvitationAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (!IsToken(token)) return null;
        var hash = HashInvitationToken(token);
        return await dbContext.TransferableInvitations.SingleOrDefaultAsync(
            item => item.TokenHash == hash,
            cancellationToken);
    }

    private string HashInvitationToken(string token) => Convert.ToHexString(
        HMACSHA256.HashData(invitationTokenPepper, Encoding.UTF8.GetBytes(token)));

    private string HashSessionToken(string token) => Convert.ToHexString(
        HMACSHA256.HashData(sessionTokenPepper, Encoding.UTF8.GetBytes(token)));

    private static bool IsToken(string token) => token is { Length: 64 } && token.All(Uri.IsHexDigit);

    private static bool IsValidIdentity(
        string email,
        string firstName,
        string lastName,
        string password,
        string passwordConfirmation) =>
        email.Length <= 320
        && MailAddress.TryCreate(email, out var parsedEmail)
        && string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase)
        && firstName is { Length: > 0 and <= 80 }
        && lastName is { Length: > 0 and <= 80 }
        && password == passwordConfirmation
        && password is not null
        && password.EnumerateRunes().Count() is >= 15 and <= 128;

    private static TenantRole ToTenantRole(CampRole role) => role switch
    {
        CampRole.CampLead => TenantRole.CampLead,
        CampRole.Member => TenantRole.Member,
        _ => TenantRole.Viewer
    };

    private static InvitationGrant ToGrant(TransferableInvitationEntity entity) => new(
        entity.IsSuperAdmin,
        entity.OrganizationId,
        entity.OrganizationRole,
        entity.CampId,
        entity.CampRole,
        entity.NewOrganizationName is not null && entity.NewOrganizationSlug is not null
            ? new OrganizationInvitationDraft(entity.NewOrganizationName, entity.NewOrganizationSlug)
            : null);

    private static byte[] RequirePepper(byte[] pepper, string purpose) => pepper.Length >= 32
        ? pepper.ToArray()
        : throw new ArgumentException($"The {purpose}-token pepper must contain at least 32 bytes.", nameof(pepper));
}

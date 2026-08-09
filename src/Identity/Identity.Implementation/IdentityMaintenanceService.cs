using Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class IdentityMaintenanceService(
    IdentityDbContext dbContext,
    TimeProvider timeProvider) : IIdentityMaintenance
{
    private static readonly TimeSpan RateEventRetention = TimeSpan.FromDays(1);
    private static readonly TimeSpan ErasureGracePeriod = TimeSpan.FromDays(30);

    public async Task<IdentityCleanupResult> CleanupExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var now = timeProvider.GetUtcNow();
        var loginChallenges = await dbContext.LoginChallenges
            .Where(item => item.ExpiresAt <= now || item.UsedAt != null)
            .OrderBy(item => item.ExpiresAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        var emailChallenges = await dbContext.EmailChangeChallenges
            .Where(item => item.ExpiresAt <= now || item.UsedAt != null)
            .OrderBy(item => item.ExpiresAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        var invitations = await dbContext.Invitations
            .Where(item => item.ExpiresAt <= now || item.RevokedAt != null || item.UsedAt != null)
            .OrderBy(item => item.ExpiresAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        var sessions = await dbContext.LoginSessions
            .Where(item => item.ExpiresAt <= now || item.RevokedAt != null)
            .OrderBy(item => item.ExpiresAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        var rateEvents = await dbContext.LoginRateEvents
            .Where(item => item.OccurredAt <= now.Subtract(RateEventRetention))
            .OrderBy(item => item.OccurredAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        dbContext.RemoveRange(loginChallenges);
        dbContext.RemoveRange(emailChallenges);
        dbContext.RemoveRange(invitations);
        dbContext.RemoveRange(sessions);
        dbContext.RemoveRange(rateEvents);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IdentityCleanupResult(
            loginChallenges.Length,
            emailChallenges.Length,
            invitations.Length,
            sessions.Length,
            rateEvents.Length);
    }

    public async Task<ErasureCandidates> ClaimDueErasuresAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var now = timeProvider.GetUtcNow();
        var dueBefore = now.Subtract(ErasureGracePeriod);
        var organizations = await dbContext.Organizations
            .Where(item => item.Status == OrganizationStatus.Erasing
                || item.DeletionScheduledAt <= dueBefore)
            .OrderBy(item => item.DeletionScheduledAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        foreach (var organization in organizations.Where(item => item.Status != OrganizationStatus.Erasing))
        {
            organization.Status = OrganizationStatus.Erasing;
            organization.Version++;
        }

        var users = await dbContext.Users
            .Where(item => item.ErasureStartedAt != null || item.DeletionScheduledAt <= dueBefore)
            .OrderBy(item => item.DeletionScheduledAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        foreach (var user in users.Where(item => item.ErasureStartedAt == null))
        {
            user.ErasureStartedAt = now;
        }

        var claimedUserIds = users.Select(item => item.Id).ToArray();
        if (claimedUserIds.Length > 0)
        {
            dbContext.LoginSessions.RemoveRange(
                dbContext.LoginSessions.Where(item => claimedUserIds.Contains(item.UserId)));
            dbContext.LoginChallenges.RemoveRange(
                dbContext.LoginChallenges.Where(item => claimedUserIds.Contains(item.UserId)));
            dbContext.EmailChangeChallenges.RemoveRange(
                dbContext.EmailChangeChallenges.Where(item => claimedUserIds.Contains(item.UserId)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ErasureCandidates(
            organizations.Select(item => item.Id).ToArray(),
            users.Select(item => item.Id).ToArray());
    }

    public async Task CompleteOrganizationErasureAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations.SingleOrDefaultAsync(
            item => item.Id == organizationId && item.Status == OrganizationStatus.Erasing,
            cancellationToken);
        if (organization is null)
        {
            return;
        }

        dbContext.CampAssignments.RemoveRange(
            dbContext.CampAssignments.Where(item => item.OrganizationId == organizationId));
        dbContext.Memberships.RemoveRange(
            dbContext.Memberships.Where(item => item.OrganizationId == organizationId));
        dbContext.Invitations.RemoveRange(
            dbContext.Invitations.Where(item => item.OrganizationId == organizationId));
        dbContext.Organizations.Remove(organization);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAccountErasureAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId && item.ErasureStartedAt != null,
            cancellationToken);
        if (user is null)
        {
            return;
        }

        dbContext.CampAssignments.RemoveRange(
            dbContext.CampAssignments.Where(item => item.UserId == userId));
        dbContext.Memberships.RemoveRange(
            dbContext.Memberships.Where(item => item.UserId == userId));
        dbContext.LoginChallenges.RemoveRange(
            dbContext.LoginChallenges.Where(item => item.UserId == userId));
        dbContext.LoginSessions.RemoveRange(
            dbContext.LoginSessions.Where(item => item.UserId == userId));
        dbContext.EmailChangeChallenges.RemoveRange(
            dbContext.EmailChangeChallenges.Where(item => item.UserId == userId));
        if (!string.IsNullOrWhiteSpace(user.NormalizedEmail))
        {
            dbContext.Invitations.RemoveRange(
                dbContext.Invitations.Where(item => item.NormalizedEmail == user.NormalizedEmail));
        }

        dbContext.RemoveRange(dbContext.Set<IdentityUserRole<Guid>>().Where(item => item.UserId == userId));
        dbContext.RemoveRange(dbContext.Set<IdentityUserClaim<Guid>>().Where(item => item.UserId == userId));
        dbContext.RemoveRange(dbContext.Set<IdentityUserLogin<Guid>>().Where(item => item.UserId == userId));
        dbContext.RemoveRange(dbContext.Set<IdentityUserToken<Guid>>().Where(item => item.UserId == userId));
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

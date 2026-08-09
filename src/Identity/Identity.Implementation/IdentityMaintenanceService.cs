using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class IdentityMaintenanceService(
    IdentityDbContext dbContext,
    TimeProvider timeProvider) : IIdentityMaintenance
{
    private static readonly TimeSpan RateEventRetention = TimeSpan.FromDays(1);

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
}

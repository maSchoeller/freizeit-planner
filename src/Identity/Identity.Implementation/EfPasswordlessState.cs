using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class EfPasswordlessState(IdentityDbContext dbContext) : IPasswordlessState
{
    public async ValueTask<KnownUser?> FindUserAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(user => user.NormalizedEmail == normalizedEmail && user.DeletionScheduledAt == null)
            .Select(user => new KnownUser(user.Id, user.NormalizedEmail!, user.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask SaveChallengeAsync(
        LoginChallenge challenge,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.LoginChallenges
            .SingleOrDefaultAsync(item => item.NormalizedEmail == challenge.NormalizedEmail, cancellationToken);
        if (entity is not null && entity.Id != challenge.Id)
        {
            dbContext.LoginChallenges.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            entity = null;
        }

        if (entity is null)
        {
            entity = new LoginChallengeEntity
            {
                Id = challenge.Id,
                UserId = challenge.UserId,
                NormalizedEmail = challenge.NormalizedEmail,
                CodeHash = challenge.CodeHash,
                ExpiresAt = challenge.ExpiresAt
            };
            dbContext.LoginChallenges.Add(entity);
        }
        else
        {
            entity.UserId = challenge.UserId;
            entity.CodeHash = challenge.CodeHash;
            entity.ExpiresAt = challenge.ExpiresAt;
            entity.FailedAttempts = challenge.FailedAttempts;
            entity.UsedAt = challenge.UsedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<LoginChallenge?> FindCurrentChallengeAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return await dbContext.LoginChallenges
            .Where(item => item.NormalizedEmail == normalizedEmail)
            .Select(item => new LoginChallenge(
                item.Id,
                item.UserId,
                item.NormalizedEmail,
                item.CodeHash,
                item.ExpiresAt,
                item.FailedAttempts,
                item.UsedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask SaveSessionAsync(LoginSession session, CancellationToken cancellationToken)
    {
        var entity = await dbContext.LoginSessions
            .SingleOrDefaultAsync(item => item.Id == session.Id, cancellationToken);
        if (entity is null)
        {
            dbContext.LoginSessions.Add(new LoginSessionEntity
            {
                Id = session.Id,
                UserId = session.UserId,
                CreatedAt = session.CreatedAt,
                ExpiresAt = session.ExpiresAt,
                IpAddress = session.IpAddress,
                RevokedAt = session.RevokedAt
            });
        }
        else
        {
            entity.RevokedAt = session.RevokedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<LoginSession?> FindSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.LoginSessions
            .Where(item => item.Id == sessionId)
            .Select(item => new LoginSession(
                item.Id,
                item.UserId,
                item.CreatedAt,
                item.ExpiresAt,
                item.IpAddress,
                item.RevokedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LoginSession>> ListSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.LoginSessions
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new LoginSession(
                item.Id,
                item.UserId,
                item.CreatedAt,
                item.ExpiresAt,
                item.IpAddress,
                item.RevokedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async ValueTask AddRateEventAsync(RateEvent rateEvent, CancellationToken cancellationToken)
    {
        dbContext.LoginRateEvents.Add(new LoginRateEventEntity
        {
            Partition = rateEvent.Partition,
            OccurredAt = rateEvent.OccurredAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<int> CountRateEventsAsync(
        string partition,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        return await dbContext.LoginRateEvents.CountAsync(
            item => item.Partition == partition && item.OccurredAt >= since,
            cancellationToken);
    }
}

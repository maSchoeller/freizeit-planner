using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class EfEmailChangeState(IdentityDbContext dbContext) : IEmailChangeState
{
    public async ValueTask<EmailChangeUser?> FindUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.Users
            .Where(item => item.Id == userId)
            .Select(item => new EmailChangeUser(item.Id, item.Email!, item.NormalizedEmail!))
            .SingleOrDefaultAsync(cancellationToken);

    public async ValueTask<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid exceptUserId,
        CancellationToken cancellationToken) =>
        await dbContext.Users.AnyAsync(
            item => item.Id != exceptUserId && item.NormalizedEmail == normalizedEmail,
            cancellationToken);

    public async ValueTask<EmailChangeChallenge?> FindChallengeAsync(
        Guid userId,
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        await dbContext.EmailChangeChallenges
            .Where(item => item.UserId == userId && item.NormalizedEmail == normalizedEmail)
            .Select(item => new EmailChangeChallenge(
                item.UserId,
                item.Email,
                item.NormalizedEmail,
                item.CodeHash,
                item.ExpiresAt,
                item.FailedAttempts,
                item.UsedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async ValueTask SaveChallengeAsync(
        EmailChangeChallenge challenge,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.EmailChangeChallenges.SingleOrDefaultAsync(
            item => item.UserId == challenge.UserId,
            cancellationToken);
        if (entity is null)
        {
            dbContext.EmailChangeChallenges.Add(ToEntity(challenge));
        }
        else
        {
            Apply(entity, challenge);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask SaveUserAndChallengeAsync(
        EmailChangeUser user,
        EmailChangeChallenge challenge,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var entity = await dbContext.Users.SingleAsync(item => item.Id == user.Id, cancellationToken);
            entity.Email = user.Email;
            entity.NormalizedEmail = user.NormalizedEmail;
            entity.UserName = user.Email;
            entity.NormalizedUserName = user.NormalizedEmail;
            entity.EmailConfirmed = true;
            entity.SecurityStamp = Guid.NewGuid().ToString("N");
            var challengeEntity = await dbContext.EmailChangeChallenges.SingleAsync(
                item => item.UserId == challenge.UserId,
                cancellationToken);
            Apply(challengeEntity, challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async ValueTask<int> CountRateEventsAsync(
        string partition,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        await dbContext.LoginRateEvents.CountAsync(
            item => item.Partition == partition && item.OccurredAt >= since,
            cancellationToken);

    public async ValueTask AddRateEventAsync(RateEvent rateEvent, CancellationToken cancellationToken)
    {
        dbContext.LoginRateEvents.Add(new LoginRateEventEntity
        {
            Partition = rateEvent.Partition,
            OccurredAt = rateEvent.OccurredAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static EmailChangeChallengeEntity ToEntity(EmailChangeChallenge challenge) => new()
    {
        UserId = challenge.UserId,
        Email = challenge.Email,
        NormalizedEmail = challenge.NormalizedEmail,
        CodeHash = challenge.CodeHash,
        ExpiresAt = challenge.ExpiresAt,
        FailedAttempts = challenge.FailedAttempts,
        UsedAt = challenge.UsedAt
    };

    private static void Apply(EmailChangeChallengeEntity entity, EmailChangeChallenge challenge)
    {
        entity.Email = challenge.Email;
        entity.NormalizedEmail = challenge.NormalizedEmail;
        entity.CodeHash = challenge.CodeHash;
        entity.ExpiresAt = challenge.ExpiresAt;
        entity.FailedAttempts = challenge.FailedAttempts;
        entity.UsedAt = challenge.UsedAt;
    }
}

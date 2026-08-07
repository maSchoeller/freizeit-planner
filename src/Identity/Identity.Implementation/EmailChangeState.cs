namespace Identity.Implementation;

public interface IEmailChangeState
{
    ValueTask<EmailChangeUser?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    ValueTask<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid exceptUserId,
        CancellationToken cancellationToken);

    ValueTask<EmailChangeChallenge?> FindChallengeAsync(
        Guid userId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    ValueTask SaveChallengeAsync(EmailChangeChallenge challenge, CancellationToken cancellationToken);

    ValueTask SaveUserAndChallengeAsync(
        EmailChangeUser user,
        EmailChangeChallenge challenge,
        CancellationToken cancellationToken);

    ValueTask<int> CountRateEventsAsync(
        string partition,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    ValueTask AddRateEventAsync(RateEvent rateEvent, CancellationToken cancellationToken);
}

public sealed class EmailChangeUser(Guid id, string email, string normalizedEmail)
{
    public Guid Id { get; } = id;

    public string Email { get; private set; } = email;

    public string NormalizedEmail { get; private set; } = normalizedEmail;

    public void ChangeEmail(string email, string normalizedEmail)
    {
        Email = email;
        NormalizedEmail = normalizedEmail;
    }
}

public sealed class EmailChangeChallenge(
    Guid userId,
    string email,
    string normalizedEmail,
    string codeHash,
    DateTimeOffset expiresAt,
    int failedAttempts = 0,
    DateTimeOffset? usedAt = null)
{
    public Guid UserId { get; } = userId;

    public string Email { get; } = email;

    public string NormalizedEmail { get; } = normalizedEmail;

    public string CodeHash { get; } = codeHash;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    public int FailedAttempts { get; private set; } = failedAttempts;

    public DateTimeOffset? UsedAt { get; private set; } = usedAt;

    public void RecordFailure() => FailedAttempts++;

    public void MarkUsed(DateTimeOffset usedAt) => UsedAt ??= usedAt;
}

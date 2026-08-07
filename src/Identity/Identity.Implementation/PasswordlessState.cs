namespace Identity.Implementation;

public interface IPasswordlessState
{
    ValueTask<KnownUser?> FindUserAsync(string normalizedEmail, CancellationToken cancellationToken);

    ValueTask SaveChallengeAsync(LoginChallenge challenge, CancellationToken cancellationToken);

    ValueTask<LoginChallenge?> FindCurrentChallengeAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    ValueTask SaveSessionAsync(LoginSession session, CancellationToken cancellationToken);

    ValueTask<LoginSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<LoginSession>> ListSessionsAsync(Guid userId, CancellationToken cancellationToken);

    ValueTask AddRateEventAsync(RateEvent rateEvent, CancellationToken cancellationToken);

    ValueTask<int> CountRateEventsAsync(
        string partition,
        DateTimeOffset since,
        CancellationToken cancellationToken);
}

public sealed record KnownUser(Guid Id, string NormalizedEmail, string DisplayName);

public sealed class LoginChallenge(
    Guid id,
    Guid userId,
    string normalizedEmail,
    string codeHash,
    DateTimeOffset expiresAt,
    int failedAttempts = 0,
    DateTimeOffset? usedAt = null)
{
    public Guid Id { get; } = id;

    public Guid UserId { get; } = userId;

    public string NormalizedEmail { get; } = normalizedEmail;

    public string CodeHash { get; } = codeHash;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    public int FailedAttempts { get; private set; } = failedAttempts;

    public DateTimeOffset? UsedAt { get; private set; } = usedAt;

    public bool HasExceededAttempts => FailedAttempts >= 5;

    public void RecordFailure() => FailedAttempts++;

    public void MarkUsed(DateTimeOffset usedAt) => UsedAt = usedAt;
}

public sealed class LoginSession(
    Guid id,
    Guid userId,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt,
    string ipAddress,
    DateTimeOffset? revokedAt = null)
{
    public Guid Id { get; } = id;

    public Guid UserId { get; } = userId;

    public DateTimeOffset CreatedAt { get; } = createdAt;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    public string IpAddress { get; } = ipAddress;

    public DateTimeOffset? RevokedAt { get; private set; } = revokedAt;

    public void Revoke(DateTimeOffset revokedAt) => RevokedAt ??= revokedAt;
}

public sealed record RateEvent(string Partition, DateTimeOffset OccurredAt);

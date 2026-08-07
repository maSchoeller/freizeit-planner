using System.Collections.Concurrent;

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

public sealed class InMemoryPasswordlessState(IEnumerable<KnownUser> users) : IPasswordlessState
{
    private readonly Dictionary<string, KnownUser> users = users.ToDictionary(
        user => user.NormalizedEmail,
        StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, LoginChallenge> challenges = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, LoginSession> sessions = new();
    private readonly ConcurrentQueue<RateEvent> rateEvents = new();

    public ValueTask<KnownUser?> FindUserAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        users.TryGetValue(normalizedEmail, out var user);
        return ValueTask.FromResult(user);
    }

    public ValueTask SaveChallengeAsync(LoginChallenge challenge, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        challenges[challenge.NormalizedEmail] = challenge;
        return ValueTask.CompletedTask;
    }

    public ValueTask<LoginChallenge?> FindCurrentChallengeAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        challenges.TryGetValue(normalizedEmail, out var challenge);
        return ValueTask.FromResult(challenge);
    }

    public ValueTask SaveSessionAsync(LoginSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sessions[session.Id] = session;
        return ValueTask.CompletedTask;
    }

    public ValueTask<LoginSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sessions.TryGetValue(sessionId, out var session);
        return ValueTask.FromResult(session);
    }

    public ValueTask<IReadOnlyList<LoginSession>> ListSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<LoginSession> result = sessions.Values
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.CreatedAt)
            .ToArray();
        return ValueTask.FromResult(result);
    }

    public ValueTask AddRateEventAsync(RateEvent rateEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        rateEvents.Enqueue(rateEvent);
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> CountRateEventsAsync(
        string partition,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = rateEvents.Count(item => item.Partition == partition && item.OccurredAt >= since);
        return ValueTask.FromResult(count);
    }
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

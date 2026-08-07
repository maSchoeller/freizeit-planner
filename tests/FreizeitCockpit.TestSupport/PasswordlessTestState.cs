using System.Collections.Concurrent;
using Identity.Implementation;

namespace FreizeitCockpit.TestSupport;

public sealed class PasswordlessTestState(IEnumerable<KnownUser> users) : IPasswordlessState
{
    private readonly Dictionary<string, KnownUser> users = users.ToDictionary(
        user => user.NormalizedEmail,
        StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, LoginChallenge> challenges = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, LoginSession> sessions = new();
    private readonly ConcurrentQueue<RateEvent> rateEvents = new();

    public static PasswordlessTestState WithMiriam() => new(
    [
        new KnownUser(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "MIRIAM@EXAMPLE.TEST",
            "Miriam König")
    ]);

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
        return ValueTask.FromResult(rateEvents.Count(item =>
            item.Partition == partition && item.OccurredAt >= since));
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Identity.Contracts;

namespace Identity.Implementation;

public sealed class PasswordlessLoginService(
    IPasswordlessState state,
    ILoginCodeSender sender,
    TimeProvider timeProvider,
    byte[] pepper) : IPasswordlessLogin
{
    private const int MaxRequestsPerWindow = 5;
    private const int MaxVerificationsPerWindow = 10;
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StandardSessionLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan RememberedSessionLifetime = TimeSpan.FromDays(30);
    private readonly byte[] pepper = pepper.Length >= 32
        ? pepper.ToArray()
        : throw new ArgumentException("The login-code pepper must contain at least 32 bytes.", nameof(pepper));

    public async Task RequestCodeAsync(LoginCodeRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var email = NormalizeEmail(request.Email);
        var emailPartition = CreateRatePartition("request:email", email);
        var ipPartition = CreateRatePartition("request:ip", request.IpAddress);
        var isLimited = await IsRateLimitedAsync(
                emailPartition,
                MaxRequestsPerWindow,
                now,
                cancellationToken)
            || await IsRateLimitedAsync(
                ipPartition,
                MaxRequestsPerWindow,
                now,
                cancellationToken);

        await state.AddRateEventAsync(new RateEvent(emailPartition, now), cancellationToken);
        await state.AddRateEventAsync(new RateEvent(ipPartition, now), cancellationToken);
        if (isLimited)
        {
            return;
        }

        var user = await state.FindUserAsync(email, cancellationToken);
        if (user is null)
        {
            return;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var id = Guid.NewGuid();
        var expiresAt = now.Add(ChallengeLifetime);
        var hash = HashCode(id, email, code);
        await state.SaveChallengeAsync(
            new LoginChallenge(id, user.Id, email, hash, expiresAt),
            cancellationToken);
        await sender.SendAsync(request.Email.Trim(), code, expiresAt, cancellationToken);
    }

    public async Task<LoginResult> VerifyCodeAsync(
        LoginCodeVerification request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var email = NormalizeEmail(request.Email);
        var emailPartition = CreateRatePartition("verify:email", email);
        var ipPartition = CreateRatePartition("verify:ip", request.IpAddress);
        if (await IsRateLimitedAsync(
                emailPartition,
                MaxVerificationsPerWindow,
                now,
                cancellationToken)
            || await IsRateLimitedAsync(
                ipPartition,
                MaxVerificationsPerWindow,
                now,
                cancellationToken))
        {
            return LoginResult.Failed(LoginOutcome.RateLimited);
        }

        await state.AddRateEventAsync(new RateEvent(emailPartition, now), cancellationToken);
        await state.AddRateEventAsync(new RateEvent(ipPartition, now), cancellationToken);

        var challenge = await state.FindCurrentChallengeAsync(email, cancellationToken);
        if (challenge is null || challenge.UsedAt is not null)
        {
            return LoginResult.Failed(LoginOutcome.InvalidCode);
        }

        if (challenge.HasExceededAttempts)
        {
            return LoginResult.Failed(LoginOutcome.AttemptsExceeded);
        }

        if (challenge.ExpiresAt <= now)
        {
            return LoginResult.Failed(LoginOutcome.Expired);
        }

        var actualHash = HashCode(challenge.Id, email, request.Code);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(challenge.CodeHash),
                Convert.FromHexString(actualHash)))
        {
            challenge.RecordFailure();
            await state.SaveChallengeAsync(challenge, cancellationToken);
            return LoginResult.Failed(
                challenge.HasExceededAttempts ? LoginOutcome.AttemptsExceeded : LoginOutcome.InvalidCode);
        }

        challenge.MarkUsed(now);
        await state.SaveChallengeAsync(challenge, cancellationToken);
        var user = await state.FindUserAsync(email, cancellationToken)
            ?? throw new InvalidOperationException("The challenge user no longer exists.");
        var session = new LoginSession(
            Guid.NewGuid(),
            user.Id,
            now,
            now.Add(request.RememberMe ? RememberedSessionLifetime : StandardSessionLifetime),
            request.IpAddress);
        await state.SaveSessionAsync(session, cancellationToken);
        return new LoginResult(
            LoginOutcome.Succeeded,
            new AuthenticatedSession(session.Id, user.Id, user.DisplayName, session.ExpiresAt));
    }

    public async Task<IReadOnlyList<SessionView>> ListSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var sessions = await state.ListSessionsAsync(userId, cancellationToken);
        return sessions
            .Where(session => session.RevokedAt is null && session.ExpiresAt > timeProvider.GetUtcNow())
            .Select(session => new SessionView(
                session.Id,
                session.CreatedAt,
                session.ExpiresAt,
                session.IpAddress,
                session.Id == currentSessionId))
            .ToArray();
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await state.FindSessionAsync(sessionId, cancellationToken);
        if (session?.UserId == userId)
        {
            session.Revoke(timeProvider.GetUtcNow());
            await state.SaveSessionAsync(session, cancellationToken);
        }
    }

    public async Task RevokeOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var sessions = await state.ListSessionsAsync(userId, cancellationToken);
        foreach (var session in sessions.Where(item => item.Id != currentSessionId))
        {
            session.Revoke(timeProvider.GetUtcNow());
            await state.SaveSessionAsync(session, cancellationToken);
        }
    }

    public async Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await state.FindSessionAsync(sessionId, cancellationToken);
        return session is { RevokedAt: null } && session.ExpiresAt > timeProvider.GetUtcNow();
    }

    private async ValueTask<bool> IsRateLimitedAsync(
        string partition,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var count = await state.CountRateEventsAsync(partition, now.Subtract(RateWindow), cancellationToken);
        return count >= limit;
    }

    private string HashCode(Guid challengeId, string normalizedEmail, string code)
    {
        var payload = Encoding.UTF8.GetBytes($"{challengeId:N}|{normalizedEmail}|{code}");
        return Convert.ToHexString(HMACSHA256.HashData(pepper, payload));
    }

    private string CreateRatePartition(string purpose, string value)
    {
        var digest = HMACSHA256.HashData(
            pepper,
            Encoding.UTF8.GetBytes($"{purpose}|{value}"));
        return $"{purpose}:{Convert.ToHexString(digest)}";
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return "INVALID";
        }

        return email.Trim().ToUpperInvariant();
    }
}

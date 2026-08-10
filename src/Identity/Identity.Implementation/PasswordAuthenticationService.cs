using System.Security.Cryptography;
using System.Text;
using Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public interface IAuthenticationTokenIssuer
{
    AuthenticationTokenPair Issue(AuthenticationTokenRequest request);
}

public interface IRefreshTokenReader
{
    RefreshTokenIdentity? Read(string token);
}

public sealed record RefreshTokenIdentity(Guid UserId, Guid SessionId, string SecurityStamp);

public sealed record AuthenticationTokenRequest(
    Guid UserId,
    Guid SessionId,
    string DisplayName,
    string SecurityStamp,
    DateTimeOffset IssuedAt,
    DateTimeOffset RefreshExpiresAt);

public sealed record AuthenticationTokenPair(
    string AccessToken,
    DateTimeOffset AccessExpiresAt,
    string RefreshToken);

public sealed class PasswordAuthenticationService(
    IdentityDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IAuthenticationTokenIssuer tokenIssuer,
    IRefreshTokenReader refreshTokenReader,
    TimeProvider timeProvider,
    byte[] ratePepper) : IPasswordAuthentication, IAuthenticationSessionValidator, IAuthenticationSessionManagement
{
    private const int MaxEmailAttempts = 10;
    private const int MaxIpAttempts = 50;
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StandardSessionLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan RememberedSessionLifetime = TimeSpan.FromDays(30);
    private readonly byte[] ratePepper = ratePepper.Length >= 32
        ? ratePepper.ToArray()
        : throw new ArgumentException("The authentication rate pepper must contain at least 32 bytes.", nameof(ratePepper));

    public async Task<PasswordAuthenticationResult> LoginAsync(
        PasswordLoginRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedEmail = NormalizeEmail(request.Email);
        var emailPartition = CreatePartition("password:email", normalizedEmail);
        var ipPartition = CreatePartition("password:ip", request.IpAddress);
        var since = now.Subtract(RateWindow);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.NormalizedEmail == normalizedEmail && item.DeletionScheduledAt == null,
            cancellationToken);
        if (user?.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            return PasswordAuthenticationResult.Failed(PasswordAuthenticationOutcome.LockedOut);
        }
        if (await CountRecentEventsAsync(emailPartition, since, cancellationToken) >= MaxEmailAttempts
            || await CountRecentEventsAsync(ipPartition, since, cancellationToken) >= MaxIpAttempts)
        {
            return PasswordAuthenticationResult.Failed(PasswordAuthenticationOutcome.RateLimited);
        }

        dbContext.LoginRateEvents.AddRange(
            new LoginRateEventEntity { Partition = emailPartition, OccurredAt = now },
            new LoginRateEventEntity { Partition = ipPartition, OccurredAt = now });
        if (user is null || !user.EmailConfirmed || string.IsNullOrEmpty(user.PasswordHash))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return PasswordAuthenticationResult.Failed(PasswordAuthenticationOutcome.InvalidCredentials);
        }
        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxEmailAttempts)
            {
                user.LockoutEnd = now.Add(LockoutDuration);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            return PasswordAuthenticationResult.Failed(
                user.LockoutEnd is not null
                    ? PasswordAuthenticationOutcome.LockedOut
                    : PasswordAuthenticationOutcome.InvalidCredentials);
        }

        if (user.AccountStatus == AccountStatus.Suspended)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return PasswordAuthenticationResult.Failed(PasswordAuthenticationOutcome.Suspended);
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        var sessionId = Guid.NewGuid();
        var refreshExpiresAt = now.Add(
            request.RememberMe ? RememberedSessionLifetime : StandardSessionLifetime);
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
            IpAddress = request.IpAddress,
            RefreshTokenHash = HashToken(pair.RefreshToken),
            RememberMe = request.RememberMe,
            ReauthenticatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return PasswordAuthenticationResult.Succeeded(new IssuedAuthentication(
            sessionId,
            new AccessTokenResponse(pair.AccessToken, pair.AccessExpiresAt),
            pair.RefreshToken,
            refreshExpiresAt,
            request.RememberMe));
    }

    public async Task<bool> IsSessionActiveAsync(
        SessionValidationRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await dbContext.LoginSessions.SingleOrDefaultAsync(
            item => item.Id == request.SessionId && item.UserId == request.UserId,
            cancellationToken);
        if (session is not { RevokedAt: null } || session.ExpiresAt <= now)
        {
            return false;
        }
        var currentUser = await dbContext.Users
            .Where(item => item.Id == request.UserId && item.DeletionScheduledAt == null)
            .Select(item => new { item.SecurityStamp, item.AccountStatus })
            .SingleOrDefaultAsync(cancellationToken);
        return currentUser is { AccountStatus: AccountStatus.Active }
            && string.Equals(currentUser.SecurityStamp, request.SecurityStamp, StringComparison.Ordinal);
    }

    public async Task<RefreshAuthenticationResult> RefreshAsync(
        RefreshAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        var identity = refreshTokenReader.Read(request.RefreshToken);
        if (identity is null)
        {
            return RefreshAuthenticationResult.Failed(RefreshAuthenticationOutcome.Invalid);
        }

        var now = timeProvider.GetUtcNow();
        var session = await dbContext.LoginSessions.SingleOrDefaultAsync(
            item => item.Id == identity.SessionId && item.UserId == identity.UserId,
            cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == identity.UserId && item.DeletionScheduledAt == null,
            cancellationToken);
        if (session is not { RevokedAt: null }
            || session.ExpiresAt <= now
            || user is null
            || user.AccountStatus == AccountStatus.Suspended
            || !string.Equals(user.SecurityStamp, identity.SecurityStamp, StringComparison.Ordinal))
        {
            return RefreshAuthenticationResult.Failed(RefreshAuthenticationOutcome.Invalid);
        }

        if (!TokenHashMatches(session.RefreshTokenHash, request.RefreshToken))
        {
            session.RevokedAt = now;
            session.Version++;
            await dbContext.SaveChangesAsync(cancellationToken);
            return RefreshAuthenticationResult.Failed(RefreshAuthenticationOutcome.Reused);
        }

        var refreshExpiresAt = session.RememberMe
            ? now.Add(RememberedSessionLifetime)
            : session.ExpiresAt;
        var pair = tokenIssuer.Issue(new AuthenticationTokenRequest(
            user.Id,
            session.Id,
            user.DisplayName,
            user.SecurityStamp ?? string.Empty,
            now,
            refreshExpiresAt));
        session.RefreshTokenHash = HashToken(pair.RefreshToken);
        session.ExpiresAt = refreshExpiresAt;
        session.IpAddress = request.IpAddress;
        session.Version++;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RefreshAuthenticationResult.Failed(RefreshAuthenticationOutcome.Invalid);
        }
        return RefreshAuthenticationResult.Succeeded(new IssuedAuthentication(
            session.Id,
            new AccessTokenResponse(pair.AccessToken, pair.AccessExpiresAt),
            pair.RefreshToken,
            refreshExpiresAt,
            session.RememberMe));
    }

    public async Task<IReadOnlyList<SessionView>> ListSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sessions = await dbContext.LoginSessions
            .Where(item => item.UserId == userId && item.RevokedAt == null)
            .ToArrayAsync(cancellationToken);
        return sessions
            .Where(item => item.ExpiresAt > now)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new SessionView(
                item.Id,
                item.CreatedAt,
                item.ExpiresAt,
                item.IpAddress,
                item.Id == currentSessionId))
            .ToArray();
    }

    public async Task RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.LoginSessions.SingleOrDefaultAsync(
            item => item.UserId == userId && item.Id == sessionId,
            cancellationToken);
        if (session is null) return;
        session.RevokedAt ??= timeProvider.GetUtcNow();
        session.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.LoginSessions
            .Where(item => item.UserId == userId && item.Id != currentSessionId && item.RevokedAt == null)
            .ToArrayAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var session in sessions)
        {
            session.RevokedAt = now;
            session.Version++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string HashToken(string token) => Convert.ToHexString(
        HMACSHA256.HashData(ratePepper, Encoding.UTF8.GetBytes(token)));

    private bool TokenHashMatches(string expectedHash, string token)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHash),
                Convert.FromHexString(HashToken(token)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string CreatePartition(string purpose, string value) => $"{purpose}:{Convert.ToHexString(
        HMACSHA256.HashData(ratePepper, Encoding.UTF8.GetBytes($"{purpose}|{value}")))}";

    private async Task<int> CountRecentEventsAsync(
        string partition,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var occurrences = await dbContext.LoginRateEvents
            .Where(item => item.Partition == partition)
            .Select(item => item.OccurredAt)
            .ToArrayAsync(cancellationToken);
        return occurrences.Count(item => item > since);
    }

    private static string NormalizeEmail(string email) =>
        string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal)
            ? "INVALID"
            : email.Trim().ToUpperInvariant();
}

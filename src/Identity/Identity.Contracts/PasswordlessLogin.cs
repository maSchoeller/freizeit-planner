namespace Identity.Contracts;

public interface IPasswordlessLogin
{
    Task RequestCodeAsync(LoginCodeRequest request, CancellationToken cancellationToken);

    Task<LoginResult> VerifyCodeAsync(LoginCodeVerification request, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionView>> ListSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken);

    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    Task RevokeOtherSessionsAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken);

    Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken cancellationToken);
}

public sealed record LoginCodeRequest(string Email, string IpAddress);

public sealed record LoginCodeVerification(string Email, string Code, string IpAddress, bool RememberMe);

public sealed record LoginResult(LoginOutcome Outcome, AuthenticatedSession? Session)
{
    public static LoginResult Failed(LoginOutcome outcome) => new(outcome, null);
}

public sealed record AuthenticatedSession(Guid Id, Guid UserId, string DisplayName, DateTimeOffset ExpiresAt);

public sealed record SessionView(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string IpAddress,
    bool IsCurrent);

public enum LoginOutcome
{
    Succeeded,
    InvalidCode,
    Expired,
    AttemptsExceeded,
    RateLimited
}

public interface ILoginCodeSender
{
    Task SendAsync(string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken);
}

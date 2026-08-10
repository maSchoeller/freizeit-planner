namespace Identity.Contracts;

public interface IPasswordAuthentication
{
    Task<PasswordAuthenticationResult> LoginAsync(
        PasswordLoginRequest request,
        CancellationToken cancellationToken);
}

public interface IAuthenticationSessionValidator
{
    Task<bool> IsSessionActiveAsync(
        SessionValidationRequest request,
        CancellationToken cancellationToken);
}

public interface IAuthenticationSessionManagement
{
    Task<RefreshAuthenticationResult> RefreshAsync(
        RefreshAuthenticationRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionView>> ListSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken);

    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    Task RevokeOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken);
}

public interface IInitialSuperAdminRegistration
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    Task<InitialSuperAdminResult> RegisterAsync(
        InitialSuperAdminRequest request,
        CancellationToken cancellationToken);
}

public interface IPasswordMaintenance
{
    Task RequestResetAsync(string email, CancellationToken cancellationToken);

    Task<PasswordResetOutcome> ConfirmResetAsync(
        PasswordResetConfirmation request,
        CancellationToken cancellationToken);

    Task<PasswordChangeOutcome> ChangePasswordAsync(
        PasswordChangeRequest request,
        CancellationToken cancellationToken);

    Task<ReauthenticationOutcome> ReauthenticateAsync(
        ReauthenticationRequest request,
        CancellationToken cancellationToken);
}

public interface IPasswordResetSender
{
    Task SendAsync(
        string email,
        string token,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}

public sealed record PasswordLoginRequest(
    string Email,
    string Password,
    string IpAddress,
    bool RememberMe);

public sealed record SessionValidationRequest(
    Guid SessionId,
    Guid UserId,
    string SecurityStamp);

public sealed record RefreshAuthenticationRequest(string RefreshToken, string IpAddress);

public sealed record RefreshAuthenticationResult(
    RefreshAuthenticationOutcome Outcome,
    IssuedAuthentication? Authentication)
{
    public static RefreshAuthenticationResult Succeeded(IssuedAuthentication authentication) =>
        new(RefreshAuthenticationOutcome.Succeeded, authentication);

    public static RefreshAuthenticationResult Failed(RefreshAuthenticationOutcome outcome) =>
        new(outcome, null);
}

public enum RefreshAuthenticationOutcome
{
    Succeeded,
    Invalid,
    Reused
}

public sealed record InitialSuperAdminRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string IpAddress);

public sealed record InitialSuperAdminResult(
    InitialSuperAdminOutcome Outcome,
    IssuedAuthentication? Authentication)
{
    public static InitialSuperAdminResult Succeeded(IssuedAuthentication authentication) =>
        new(InitialSuperAdminOutcome.Succeeded, authentication);

    public static InitialSuperAdminResult Failed(InitialSuperAdminOutcome outcome) =>
        new(outcome, null);
}

public enum InitialSuperAdminOutcome
{
    Succeeded,
    AlreadyInitialized,
    InvalidInput
}

public sealed record FirstLoginAvailability(bool Available);

public enum AccountStatus
{
    Active,
    Suspended
}

public sealed record PasswordResetConfirmation(string Token, string NewPassword);

public sealed record PasswordChangeRequest(
    Guid UserId,
    Guid SessionId,
    long ExpectedVersion,
    string CurrentPassword,
    string NewPassword);

public sealed record ReauthenticationRequest(
    Guid UserId,
    Guid SessionId,
    string Password);

public enum PasswordResetOutcome
{
    Succeeded,
    Invalid,
    InvalidPassword
}

public enum PasswordChangeOutcome
{
    Succeeded,
    InvalidCurrentPassword,
    InvalidPassword,
    Suspended,
    VersionConflict
}

public enum ReauthenticationOutcome
{
    Succeeded,
    InvalidCredentials,
    Suspended
}

public sealed record ReauthenticationResponse(DateTimeOffset ValidUntil);

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record IssuedAuthentication(
    Guid SessionId,
    AccessTokenResponse Access,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt,
    bool RememberMe);

public sealed record PasswordAuthenticationResult(
    PasswordAuthenticationOutcome Outcome,
    IssuedAuthentication? Authentication)
{
    public static PasswordAuthenticationResult Succeeded(IssuedAuthentication authentication) =>
        new(PasswordAuthenticationOutcome.Succeeded, authentication);

    public static PasswordAuthenticationResult Failed(PasswordAuthenticationOutcome outcome) =>
        new(outcome, null);
}

public enum PasswordAuthenticationOutcome
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
    RateLimited,
    Suspended
}

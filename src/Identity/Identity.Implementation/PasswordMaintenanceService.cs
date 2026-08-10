using System.Security.Cryptography;
using System.Text;
using Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class PasswordMaintenanceService(
    IdentityDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IPasswordResetSender sender,
    TimeProvider timeProvider,
    byte[] tokenPepper) : IPasswordMaintenance
{
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan ReauthenticationLifetime = TimeSpan.FromMinutes(10);
    private readonly byte[] tokenPepper = tokenPepper.Length >= 32
        ? tokenPepper.ToArray()
        : throw new ArgumentException(
            "The password-reset pepper must contain at least 32 bytes.",
            nameof(tokenPepper));

    public async Task RequestResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.NormalizedEmail == normalizedEmail && item.DeletionScheduledAt == null,
            cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return;

        var now = timeProvider.GetUtcNow();
        var previousTokens = await dbContext.PasswordResetTokens
            .Where(item => item.UserId == user.Id && item.UsedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var previousToken in previousTokens)
        {
            previousToken.UsedAt = now;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.Add(ResetLifetime);
        dbContext.PasswordResetTokens.Add(new PasswordResetTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await sender.SendAsync(user.Email, token, expiresAt, cancellationToken);
    }

    public async Task<PasswordResetOutcome> ConfirmResetAsync(
        PasswordResetConfirmation request,
        CancellationToken cancellationToken)
    {
        if (!IsValidPassword(request.NewPassword) || string.IsNullOrWhiteSpace(request.Token))
        {
            return string.IsNullOrWhiteSpace(request.Token)
                ? PasswordResetOutcome.Invalid
                : PasswordResetOutcome.InvalidPassword;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(request.Token);
        var token = await dbContext.PasswordResetTokens.SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash,
            cancellationToken);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= now)
        {
            return PasswordResetOutcome.Invalid;
        }
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == token.UserId && item.DeletionScheduledAt == null,
            cancellationToken);
        if (user is null) return PasswordResetOutcome.Invalid;

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.Version++;
        token.UsedAt = now;
        await RevokeSessionsAsync(user.Id, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return PasswordResetOutcome.Succeeded;
    }

    public async Task<PasswordChangeOutcome> ChangePasswordAsync(
        PasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidPassword(request.NewPassword)) return PasswordChangeOutcome.InvalidPassword;
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == request.UserId && item.DeletionScheduledAt == null,
            cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return PasswordChangeOutcome.InvalidCurrentPassword;
        }
        if (user.AccountStatus == AccountStatus.Suspended) return PasswordChangeOutcome.Suspended;
        if (user.Version != request.ExpectedVersion) return PasswordChangeOutcome.VersionConflict;
        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            return PasswordChangeOutcome.InvalidCurrentPassword;
        }

        var now = timeProvider.GetUtcNow();
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.Version++;
        await RevokeSessionsAsync(user.Id, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return PasswordChangeOutcome.Succeeded;
    }

    public async Task<ReauthenticationOutcome> ReauthenticateAsync(
        ReauthenticationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == request.UserId && item.DeletionScheduledAt == null,
            cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return ReauthenticationOutcome.InvalidCredentials;
        }
        if (user.AccountStatus == AccountStatus.Suspended) return ReauthenticationOutcome.Suspended;
        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        {
            return ReauthenticationOutcome.InvalidCredentials;
        }

        var session = await dbContext.LoginSessions.SingleOrDefaultAsync(
            item => item.Id == request.SessionId
                && item.UserId == request.UserId
                && item.RevokedAt == null,
            cancellationToken);
        if (session is null || session.ExpiresAt <= timeProvider.GetUtcNow())
        {
            return ReauthenticationOutcome.InvalidCredentials;
        }
        session.ReauthenticatedAt = timeProvider.GetUtcNow();
        session.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ReauthenticationOutcome.Succeeded;
    }

    public DateTimeOffset GetReauthenticationValidUntil() =>
        timeProvider.GetUtcNow().Add(ReauthenticationLifetime);

    private async Task RevokeSessionsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.LoginSessions
            .Where(item => item.UserId == userId && item.RevokedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAt = now;
            session.Version++;
        }
    }

    private string HashToken(string token) => Convert.ToHexString(
        HMACSHA256.HashData(tokenPepper, Encoding.UTF8.GetBytes(token)));

    private static bool IsValidPassword(string password)
    {
        if (password is null) return false;
        var length = password.EnumerateRunes().Count();
        return length is >= 15 and <= 128;
    }

    private static string NormalizeEmail(string email) =>
        string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal)
            ? "INVALID"
            : email.Trim().ToUpperInvariant();
}

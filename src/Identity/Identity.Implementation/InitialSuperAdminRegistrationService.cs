using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Identity.Implementation;

public sealed class InitialSuperAdminRegistrationService(
    IdentityDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IAuthenticationTokenIssuer tokenIssuer,
    TimeProvider timeProvider,
    byte[] tokenPepper) : IInitialSuperAdminRegistration
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private const long RegistrationLockId = 0x4652435354464952;
    private readonly byte[] tokenPepper = tokenPepper.Length >= 32
        ? tokenPepper.ToArray()
        : throw new ArgumentException(
            "The authentication token pepper must contain at least 32 bytes.",
            nameof(tokenPepper));

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        dbContext.Users.AllAsync(_ => false, cancellationToken);

    public async Task<InitialSuperAdminResult> RegisterAsync(
        InitialSuperAdminRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (!IsValid(email, firstName, lastName, request.Password))
        {
            return InitialSuperAdminResult.Failed(InitialSuperAdminOutcome.InvalidInput);
        }

        IDbContextTransaction? ownedTransaction = null;
        if (dbContext.Database.IsNpgsql())
        {
            if (dbContext.Database.CurrentTransaction is null)
            {
                ownedTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            }
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({RegistrationLockId})",
                cancellationToken);
        }

        try
        {
            if (await dbContext.Users.AnyAsync(cancellationToken))
            {
                return InitialSuperAdminResult.Failed(InitialSuperAdminOutcome.AlreadyInitialized);
            }

            var now = timeProvider.GetUtcNow();
            var normalizedEmail = email.ToUpperInvariant();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                NormalizedUserName = normalizedEmail,
                Email = email,
                NormalizedEmail = normalizedEmail,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                DisplayName = $"{firstName} {lastName}",
                IsSuperAdmin = true,
                // Keep legacy authorization working until the role migration removes this property.
                IsPlatformAdmin = true,
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            var sessionId = Guid.NewGuid();
            var refreshExpiresAt = now.Add(SessionLifetime);
            var pair = tokenIssuer.Issue(new AuthenticationTokenRequest(
                user.Id,
                sessionId,
                user.DisplayName,
                user.SecurityStamp,
                now,
                refreshExpiresAt));
            dbContext.Users.Add(user);
            dbContext.LoginSessions.Add(new LoginSessionEntity
            {
                Id = sessionId,
                UserId = user.Id,
                CreatedAt = now,
                ExpiresAt = refreshExpiresAt,
                IpAddress = request.IpAddress,
                RefreshTokenHash = HashToken(pair.RefreshToken),
                RememberMe = true,
                ReauthenticatedAt = now
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }
            return InitialSuperAdminResult.Succeeded(new IssuedAuthentication(
                sessionId,
                new AccessTokenResponse(pair.AccessToken, pair.AccessExpiresAt),
                pair.RefreshToken,
                refreshExpiresAt,
                true));
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    private string HashToken(string token) => Convert.ToHexString(
        HMACSHA256.HashData(tokenPepper, Encoding.UTF8.GetBytes(token)));

    private static bool IsValid(string email, string firstName, string lastName, string password) =>
        email.Length <= 320
        && MailAddress.TryCreate(email, out var parsedEmail)
        && string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase)
        && firstName is { Length: > 0 and <= 80 }
        && lastName is { Length: > 0 and <= 80 }
        && password is { Length: >= 15 and <= 128 };
}

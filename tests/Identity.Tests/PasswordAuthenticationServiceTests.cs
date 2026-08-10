using Identity.Contracts;
using Identity.Implementation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class PasswordAuthenticationServiceTests
{
    [Fact]
    public void AuthenticationSecretsMustContainAtLeastThirtyTwoBytes()
    {
        Assert.Throws<ArgumentException>(() => new PasswordAuthenticationService(
            null!,
            null!,
            null!,
            null!,
            TimeProvider.System,
            [1, 2, 3]));
        Assert.Throws<ArgumentException>(() => new InitialSuperAdminRegistrationService(
            null!,
            null!,
            null!,
            TimeProvider.System,
            [1, 2, 3]));
    }

    [Fact]
    public async Task ValidPasswordCreatesTwelveHourRevocableSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var user = new ApplicationUser
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            UserName = "miriam@example.test",
            NormalizedUserName = "MIRIAM@EXAMPLE.TEST",
            Email = "miriam@example.test",
            NormalizedEmail = "MIRIAM@EXAMPLE.TEST",
            EmailConfirmed = true,
            DisplayName = "Miriam König",
            SecurityStamp = "security-stamp",
            LockoutEnabled = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Eine sichere Testpassphrase");
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var service = new PasswordAuthenticationService(
            database,
            passwordHasher,
            new FixedTokenIssuer(),
            new FixedTokenIssuer(),
            new FixedTimeProvider(now),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

        var result = await service.LoginAsync(
            new PasswordLoginRequest(
                " MIRIAM@example.test ",
                "Eine sichere Testpassphrase",
                "192.0.2.10",
                false),
            cancellationToken);

        var authentication = Assert.IsType<IssuedAuthentication>(result.Authentication);
        Assert.Equal(PasswordAuthenticationOutcome.Succeeded, result.Outcome);
        Assert.Equal(now.AddHours(12), authentication.RefreshExpiresAt);
        Assert.False(authentication.RememberMe);
        Assert.True(await service.IsSessionActiveAsync(
            new SessionValidationRequest(authentication.SessionId, user.Id, "security-stamp"),
            cancellationToken));

        user.AccountStatus = AccountStatus.Suspended;
        await database.SaveChangesAsync(cancellationToken);
        Assert.False(await service.IsSessionActiveAsync(
            new SessionValidationRequest(authentication.SessionId, user.Id, "security-stamp"),
            cancellationToken));
        var suspended = await service.LoginAsync(
            new PasswordLoginRequest(
                "miriam@example.test",
                "Eine sichere Testpassphrase",
                "192.0.2.11",
                false),
            cancellationToken);
        Assert.Equal(PasswordAuthenticationOutcome.Suspended, suspended.Outcome);
    }

    [Fact]
    public async Task TenthFailedPasswordLocksAccountForExactlyFifteenMinutes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var user = new ApplicationUser
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            UserName = "team@example.test",
            NormalizedUserName = "TEAM@EXAMPLE.TEST",
            Email = "team@example.test",
            NormalizedEmail = "TEAM@EXAMPLE.TEST",
            EmailConfirmed = true,
            DisplayName = "Team Mitglied",
            SecurityStamp = "security-stamp",
            LockoutEnabled = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Eine sichere Testpassphrase");
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
        var clock = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var service = new PasswordAuthenticationService(
            database,
            passwordHasher,
            new FixedTokenIssuer(),
            new FixedTokenIssuer(),
            clock,
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

        PasswordAuthenticationResult? failure = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            failure = await service.LoginAsync(
                new PasswordLoginRequest(
                    "team@example.test",
                    "Falsches Passwort",
                    "192.0.2.20",
                    false),
                cancellationToken);
        }

        Assert.Equal(PasswordAuthenticationOutcome.LockedOut, failure?.Outcome);
        var whileLocked = await service.LoginAsync(
            new PasswordLoginRequest(
                "team@example.test",
                "Eine sichere Testpassphrase",
                "192.0.2.21",
                false),
            cancellationToken);
        Assert.Equal(PasswordAuthenticationOutcome.LockedOut, whileLocked.Outcome);

        clock.Advance(TimeSpan.FromMinutes(15));
        var afterLockout = await service.LoginAsync(
            new PasswordLoginRequest(
                "team@example.test",
                "Eine sichere Testpassphrase",
                "192.0.2.21",
                false),
            cancellationToken);

        Assert.Equal(PasswordAuthenticationOutcome.Succeeded, afterLockout.Outcome);
    }

    [Fact]
    public async Task RefreshRotatesTokenAndReuseRevokesSessionFamily()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var user = new ApplicationUser
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            UserName = "refresh@example.test",
            NormalizedUserName = "REFRESH@EXAMPLE.TEST",
            Email = "refresh@example.test",
            NormalizedEmail = "REFRESH@EXAMPLE.TEST",
            EmailConfirmed = true,
            DisplayName = "Refresh Test",
            SecurityStamp = "security-stamp",
            LockoutEnabled = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Eine sichere Testpassphrase");
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var tokens = new RotatingTokenService();
        var service = new PasswordAuthenticationService(
            database,
            passwordHasher,
            tokens,
            tokens,
            new FixedTimeProvider(now),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var login = await service.LoginAsync(
            new PasswordLoginRequest(
                "refresh@example.test",
                "Eine sichere Testpassphrase",
                "192.0.2.30",
                true),
            cancellationToken);
        var initial = Assert.IsType<IssuedAuthentication>(login.Authentication);

        var refreshed = await service.RefreshAsync(
            new RefreshAuthenticationRequest(initial.RefreshToken, "192.0.2.31"),
            cancellationToken);

        var rotated = Assert.IsType<IssuedAuthentication>(refreshed.Authentication);
        Assert.Equal(RefreshAuthenticationOutcome.Succeeded, refreshed.Outcome);
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);

        user.AccountStatus = AccountStatus.Suspended;
        await database.SaveChangesAsync(cancellationToken);
        var suspendedRefresh = await service.RefreshAsync(
            new RefreshAuthenticationRequest(rotated.RefreshToken, "192.0.2.31"),
            cancellationToken);
        Assert.Equal(RefreshAuthenticationOutcome.Invalid, suspendedRefresh.Outcome);
        user.AccountStatus = AccountStatus.Active;
        await database.SaveChangesAsync(cancellationToken);

        var replay = await service.RefreshAsync(
            new RefreshAuthenticationRequest(initial.RefreshToken, "192.0.2.32"),
            cancellationToken);

        Assert.Equal(RefreshAuthenticationOutcome.Reused, replay.Outcome);
        Assert.False(await service.IsSessionActiveAsync(
            new SessionValidationRequest(initial.SessionId, user.Id, "security-stamp"),
            cancellationToken));
    }

    [Fact]
    public async Task UnknownAccountIsRateLimitedWithoutRevealingItsExistence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var tokens = new FixedTokenIssuer();
        var service = new PasswordAuthenticationService(
            database,
            new PasswordHasher<ApplicationUser>(),
            tokens,
            tokens,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero)),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

        PasswordAuthenticationResult? first = null;
        PasswordAuthenticationResult? limited = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            var result = await service.LoginAsync(
                new PasswordLoginRequest(
                    "unknown@example.test",
                    "Eine sichere Testpassphrase",
                    $"192.0.2.{attempt}",
                    false),
                cancellationToken);
            first ??= result;
            limited = result;
        }

        Assert.Equal(
            PasswordAuthenticationOutcome.InvalidCredentials,
            Assert.IsType<PasswordAuthenticationResult>(first).Outcome);
        Assert.Equal(PasswordAuthenticationOutcome.RateLimited, limited?.Outcome);
    }

    [Fact]
    public async Task StandardSessionKeepsFixedExpiryAndCanRevokeOtherSessions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var user = new ApplicationUser
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
            UserName = "sessions@example.test",
            NormalizedUserName = "SESSIONS@EXAMPLE.TEST",
            Email = "sessions@example.test",
            NormalizedEmail = "SESSIONS@EXAMPLE.TEST",
            EmailConfirmed = true,
            DisplayName = "Session Test",
            SecurityStamp = "security-stamp",
            LockoutEnabled = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Eine sichere Testpassphrase");
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var tokens = new RotatingTokenService();
        var service = new PasswordAuthenticationService(
            database,
            passwordHasher,
            tokens,
            tokens,
            new FixedTimeProvider(now),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var request = new PasswordLoginRequest(
            "sessions@example.test",
            "Eine sichere Testpassphrase",
            "192.0.2.40",
            false);
        var current = Assert.IsType<IssuedAuthentication>(
            (await service.LoginAsync(request, cancellationToken)).Authentication);
        var other = Assert.IsType<IssuedAuthentication>(
            (await service.LoginAsync(request, cancellationToken)).Authentication);

        var refreshed = await service.RefreshAsync(
            new RefreshAuthenticationRequest(current.RefreshToken, "192.0.2.41"),
            cancellationToken);
        var invalidRefresh = await service.RefreshAsync(
            new RefreshAuthenticationRequest("not-a-refresh-token", "192.0.2.42"),
            cancellationToken);
        var rotated = Assert.IsType<IssuedAuthentication>(refreshed.Authentication);
        var views = await service.ListSessionsAsync(user.Id, current.SessionId, cancellationToken);
        await service.RevokeOtherSessionsAsync(user.Id, current.SessionId, cancellationToken);
        await service.RevokeSessionAsync(user.Id, Guid.NewGuid(), cancellationToken);

        Assert.Equal(current.RefreshExpiresAt, rotated.RefreshExpiresAt);
        Assert.Equal(RefreshAuthenticationOutcome.Invalid, invalidRefresh.Outcome);
        Assert.Equal(2, views.Count);
        Assert.Contains(views, item => item.IsCurrent);
        Assert.False(await service.IsSessionActiveAsync(
            new SessionValidationRequest(other.SessionId, user.Id, "security-stamp"),
            cancellationToken));
        Assert.Equal(
            RefreshAuthenticationOutcome.Invalid,
            (await service.RefreshAsync(
                new RefreshAuthenticationRequest(other.RefreshToken, "192.0.2.43"),
                cancellationToken)).Outcome);
        Assert.False(await service.IsSessionActiveAsync(
            new SessionValidationRequest(current.SessionId, user.Id, "wrong-stamp"),
            cancellationToken));
        Assert.True(await service.IsSessionActiveAsync(
            new SessionValidationRequest(current.SessionId, user.Id, "security-stamp"),
            cancellationToken));

        await service.RevokeSessionAsync(user.Id, current.SessionId, cancellationToken);
        await service.RevokeSessionAsync(user.Id, current.SessionId, cancellationToken);
        Assert.False(await service.IsSessionActiveAsync(
            new SessionValidationRequest(current.SessionId, user.Id, "security-stamp"),
            cancellationToken));
    }

    private sealed class FixedTokenIssuer : IAuthenticationTokenIssuer, IRefreshTokenReader
    {
        public AuthenticationTokenPair Issue(AuthenticationTokenRequest request) => new(
            "access.jwt.value",
            request.IssuedAt.AddMinutes(15),
            "refresh.jwt.value");

        public RefreshTokenIdentity? Read(string token) => null;
    }

    private sealed class RotatingTokenService : IAuthenticationTokenIssuer, IRefreshTokenReader
    {
        private readonly Dictionary<string, RefreshTokenIdentity> tokens = [];
        private int sequence;

        public AuthenticationTokenPair Issue(AuthenticationTokenRequest request)
        {
            var refresh = $"refresh.jwt.{++sequence}";
            tokens[refresh] = new RefreshTokenIdentity(
                request.UserId,
                request.SessionId,
                request.SecurityStamp);
            return new AuthenticationTokenPair(
                $"access.jwt.{sequence}",
                request.IssuedAt.AddMinutes(15),
                refresh);
        }

        public RefreshTokenIdentity? Read(string token) =>
            tokens.GetValueOrDefault(token);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}

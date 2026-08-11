using Identity.Contracts;
using Identity.Implementation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class InitialSuperAdminRegistrationTests
{
    [Fact]
    public async Task FirstRegistrationCreatesOnlySuperAdminAndRememberedSession()
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
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var service = new InitialSuperAdminRegistrationService(
            database,
            passwordHasher,
            new FixedTokenIssuer(),
            new FixedTimeProvider(now),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

        Assert.True(await service.IsAvailableAsync(cancellationToken));

        var result = await service.RegisterAsync(
            new InitialSuperAdminRequest(
                " admin@example.test ",
                "Eine sichere Admin-Passphrase",
                " Ada ",
                " Lovelace ",
                "192.0.2.1"),
            cancellationToken);

        var authentication = Assert.IsType<IssuedAuthentication>(result.Authentication);
        Assert.Equal(InitialSuperAdminOutcome.Succeeded, result.Outcome);
        Assert.True(authentication.RememberMe);
        Assert.Equal(now.AddDays(30), authentication.RefreshExpiresAt);
        var user = await database.Users.SingleAsync(cancellationToken);
        Assert.Equal("admin@example.test", user.Email);
        Assert.Equal("ADMIN@EXAMPLE.TEST", user.NormalizedEmail);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        Assert.Equal("Ada Lovelace", user.DisplayName);
        Assert.True(user.EmailConfirmed);
        Assert.True(user.IsSuperAdmin);
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed, passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            "Eine sichere Admin-Passphrase"));
        Assert.False(await service.IsAvailableAsync(cancellationToken));
        database.Users.Remove(user);
        await database.SaveChangesAsync(cancellationToken);
        Assert.False(await service.IsAvailableAsync(cancellationToken));
    }

    [Fact]
    public async Task FurtherRegistrationIsRejectedWithoutCreatingAnotherUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var service = new InitialSuperAdminRegistrationService(
            database,
            new PasswordHasher<ApplicationUser>(),
            new FixedTokenIssuer(),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero)),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var first = new InitialSuperAdminRequest(
            "first@example.test",
            "Eine sichere Admin-Passphrase",
            "Erste",
            "Person",
            "192.0.2.1");
        var second = first with { Email = "second@example.test", FirstName = "Zweite" };

        await service.RegisterAsync(first, cancellationToken);
        var result = await service.RegisterAsync(second, cancellationToken);

        Assert.Equal(InitialSuperAdminOutcome.AlreadyInitialized, result.Outcome);
        Assert.Null(result.Authentication);
        Assert.Equal(1, await database.Users.CountAsync(cancellationToken));
    }

    [Theory]
    [InlineData("invalid", "Ada", "Lovelace", "Eine sichere Admin-Passphrase")]
    [InlineData("admin@example.test", "", "Lovelace", "Eine sichere Admin-Passphrase")]
    [InlineData("admin@example.test", "Ada", "", "Eine sichere Admin-Passphrase")]
    [InlineData("admin@example.test", "Ada", "Lovelace", "zu kurz")]
    public async Task InvalidRegistrationIsRejectedWithoutReservingFirstLogin(
        string email,
        string firstName,
        string lastName,
        string password)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var service = new InitialSuperAdminRegistrationService(
            database,
            new PasswordHasher<ApplicationUser>(),
            new FixedTokenIssuer(),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero)),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

        var result = await service.RegisterAsync(
            new InitialSuperAdminRequest(
                email,
                password,
                firstName,
                lastName,
                "192.0.2.1"),
            cancellationToken);

        Assert.Equal(InitialSuperAdminOutcome.InvalidInput, result.Outcome);
        Assert.True(await service.IsAvailableAsync(cancellationToken));
    }

    [Fact]
    public async Task ExcessiveIdentityFieldsAreRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var service = new InitialSuperAdminRegistrationService(
            database,
            new PasswordHasher<ApplicationUser>(),
            new FixedTokenIssuer(),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero)),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var valid = new InitialSuperAdminRequest(
            "admin@example.test",
            "Eine sichere Admin-Passphrase",
            "Ada",
            "Lovelace",
            "192.0.2.1");

        var results = new[]
        {
            await service.RegisterAsync(valid with { Email = $"{new string('a', 310)}@example.test" }, cancellationToken),
            await service.RegisterAsync(valid with { FirstName = new string('a', 81) }, cancellationToken),
            await service.RegisterAsync(valid with { LastName = new string('a', 81) }, cancellationToken),
            await service.RegisterAsync(valid with { Password = new string('a', 129) }, cancellationToken)
        };

        Assert.All(results, result => Assert.Equal(InitialSuperAdminOutcome.InvalidInput, result.Outcome));
    }

    private sealed class FixedTokenIssuer : IAuthenticationTokenIssuer
    {
        public AuthenticationTokenPair Issue(AuthenticationTokenRequest request) => new(
            "access.jwt.value",
            request.IssuedAt.AddMinutes(15),
            "refresh.jwt.value");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

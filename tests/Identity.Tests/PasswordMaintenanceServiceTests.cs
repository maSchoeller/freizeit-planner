using Identity.Contracts;
using Identity.Implementation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class PasswordMaintenanceServiceTests
{
    [Fact]
    public void ResetSecretMustContainAtLeastThirtyTwoBytes()
    {
        Assert.Throws<ArgumentException>(() => new PasswordMaintenanceService(
            null!,
            null!,
            null!,
            TimeProvider.System,
            [1, 2, 3]));
    }

    [Fact]
    public async Task ResetIsGenericOneTimeAndRevokesEverySession()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        fixture.User.AccessFailedCount = 9;
        fixture.User.LockoutEnd = fixture.Now.AddMinutes(15);
        fixture.Database.LoginSessions.Add(new LoginSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = fixture.User.Id,
            CreatedAt = fixture.Now,
            ExpiresAt = fixture.Now.AddDays(1),
            IpAddress = "192.0.2.1",
            RefreshTokenHash = "hash",
            ReauthenticatedAt = fixture.Now
        });
        await fixture.Database.SaveChangesAsync(cancellationToken);

        await fixture.Service.RequestResetAsync("unknown@example.test", cancellationToken);
        Assert.Empty(fixture.Sender.Messages);
        await fixture.Service.RequestResetAsync(" USER@example.test ", cancellationToken);

        var reset = Assert.Single(fixture.Sender.Messages);
        Assert.Equal(fixture.Now.AddHours(1), reset.ExpiresAt);
        Assert.DoesNotContain(
            await fixture.Database.PasswordResetTokens.Select(item => item.TokenHash)
                .ToArrayAsync(cancellationToken),
            hash => hash.Contains(reset.Token, StringComparison.Ordinal));
        var outcome = await fixture.Service.ConfirmResetAsync(
            new PasswordResetConfirmation(reset.Token, "Eine vollständig neue Passphrase"),
            cancellationToken);
        var reused = await fixture.Service.ConfirmResetAsync(
            new PasswordResetConfirmation(reset.Token, "Noch eine sichere neue Passphrase"),
            cancellationToken);

        Assert.Equal(PasswordResetOutcome.Succeeded, outcome);
        Assert.Equal(PasswordResetOutcome.Invalid, reused);
        Assert.Equal(0, fixture.User.AccessFailedCount);
        Assert.Null(fixture.User.LockoutEnd);
        Assert.All(
            await fixture.Database.LoginSessions.ToArrayAsync(cancellationToken),
            session => Assert.NotNull(session.RevokedAt));
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            fixture.Hasher.VerifyHashedPassword(
                fixture.User,
                fixture.User.PasswordHash!,
                "Eine vollständig neue Passphrase"));
    }

    [Fact]
    public async Task ExpiredResetAndInvalidPasswordAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.Service.RequestResetAsync("user@example.test", cancellationToken);
        var reset = Assert.Single(fixture.Sender.Messages);

        var shortPassword = await fixture.Service.ConfirmResetAsync(
            new PasswordResetConfirmation(reset.Token, "zu kurz"),
            cancellationToken);
        var missingPassword = await fixture.Service.ConfirmResetAsync(
            new PasswordResetConfirmation(reset.Token, null!),
            cancellationToken);
        var excessivePassword = await fixture.Service.ConfirmResetAsync(
            new PasswordResetConfirmation(reset.Token, new string('x', 129)),
            cancellationToken);
        var missingToken = await fixture.Service.ConfirmResetAsync(
            new PasswordResetConfirmation("", "Eine ausreichend lange Passphrase"),
            cancellationToken);
        var unknownToken = await fixture.Service.ConfirmResetAsync(
            new PasswordResetConfirmation("A1B2C3", "Eine ausreichend lange Passphrase"),
            cancellationToken);
        fixture.Clock.Advance(TimeSpan.FromMinutes(60));
        var expired = await fixture.Service.ConfirmResetAsync(
            new PasswordResetConfirmation(reset.Token, "Eine ausreichend lange Passphrase"),
            cancellationToken);

        Assert.Equal(PasswordResetOutcome.InvalidPassword, shortPassword);
        Assert.Equal(PasswordResetOutcome.InvalidPassword, missingPassword);
        Assert.Equal(PasswordResetOutcome.InvalidPassword, excessivePassword);
        Assert.Equal(PasswordResetOutcome.Invalid, missingToken);
        Assert.Equal(PasswordResetOutcome.Invalid, unknownToken);
        Assert.Equal(PasswordResetOutcome.Invalid, expired);
    }

    [Fact]
    public async Task PasswordChangeRequiresCurrentPasswordAndRevokesSession()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = fixture.AddSession();
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var invalidNewPassword = await fixture.Service.ChangePasswordAsync(
            new PasswordChangeRequest(
                fixture.User.Id,
                session.Id,
                fixture.User.Version,
                Fixture.Password,
                "zu kurz"),
            cancellationToken);
        var conflict = await fixture.Service.ChangePasswordAsync(
            new PasswordChangeRequest(
                fixture.User.Id,
                session.Id,
                fixture.User.Version + 1,
                Fixture.Password,
                "Eine ausreichend lange neue Passphrase"),
            cancellationToken);
        fixture.User.AccountStatus = AccountStatus.Suspended;
        await fixture.Database.SaveChangesAsync(cancellationToken);
        var suspended = await fixture.Service.ChangePasswordAsync(
            new PasswordChangeRequest(
                fixture.User.Id,
                session.Id,
                fixture.User.Version,
                Fixture.Password,
                "Eine ausreichend lange neue Passphrase"),
            cancellationToken);
        fixture.User.AccountStatus = AccountStatus.Active;
        await fixture.Database.SaveChangesAsync(cancellationToken);
        var invalid = await fixture.Service.ChangePasswordAsync(
            new PasswordChangeRequest(
                fixture.User.Id,
                session.Id,
                fixture.User.Version,
                "falsch",
                "Eine ausreichend lange neue Passphrase"),
            cancellationToken);
        var changed = await fixture.Service.ChangePasswordAsync(
            new PasswordChangeRequest(
                fixture.User.Id,
                session.Id,
                fixture.User.Version,
                Fixture.Password,
                "Eine ausreichend lange neue Passphrase"),
            cancellationToken);

        Assert.Equal(PasswordChangeOutcome.InvalidPassword, invalidNewPassword);
        Assert.Equal(PasswordChangeOutcome.VersionConflict, conflict);
        Assert.Equal(PasswordChangeOutcome.Suspended, suspended);
        Assert.Equal(PasswordChangeOutcome.InvalidCurrentPassword, invalid);
        Assert.Equal(PasswordChangeOutcome.Succeeded, changed);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task ReauthenticationUpdatesOnlyAValidActiveSession()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = fixture.AddSession();
        session.ReauthenticatedAt = fixture.Now.AddHours(-1);
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var invalid = await fixture.Service.ReauthenticateAsync(
            new ReauthenticationRequest(fixture.User.Id, session.Id, "falsch"),
            cancellationToken);
        var missingSession = await fixture.Service.ReauthenticateAsync(
            new ReauthenticationRequest(fixture.User.Id, Guid.NewGuid(), Fixture.Password),
            cancellationToken);
        var succeeded = await fixture.Service.ReauthenticateAsync(
            new ReauthenticationRequest(fixture.User.Id, session.Id, Fixture.Password),
            cancellationToken);
        fixture.User.AccountStatus = AccountStatus.Suspended;
        await fixture.Database.SaveChangesAsync(cancellationToken);
        var suspended = await fixture.Service.ReauthenticateAsync(
            new ReauthenticationRequest(fixture.User.Id, session.Id, Fixture.Password),
            cancellationToken);

        Assert.Equal(ReauthenticationOutcome.InvalidCredentials, invalid);
        Assert.Equal(ReauthenticationOutcome.InvalidCredentials, missingSession);
        Assert.Equal(ReauthenticationOutcome.Succeeded, succeeded);
        Assert.Equal(fixture.Now, session.ReauthenticatedAt);
        Assert.Equal(ReauthenticationOutcome.Suspended, suspended);
    }

    [Fact]
    public async Task MissingAccountsAndExpiredSessionsAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = fixture.AddSession();
        session.ExpiresAt = fixture.Now;
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var missingPasswordChange = await fixture.Service.ChangePasswordAsync(
            new PasswordChangeRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                Fixture.Password,
                "Eine ausreichend lange neue Passphrase"),
            cancellationToken);
        var missingReauthentication = await fixture.Service.ReauthenticateAsync(
            new ReauthenticationRequest(Guid.NewGuid(), Guid.NewGuid(), Fixture.Password),
            cancellationToken);
        var expiredSession = await fixture.Service.ReauthenticateAsync(
            new ReauthenticationRequest(fixture.User.Id, session.Id, Fixture.Password),
            cancellationToken);
        fixture.User.PasswordHash = null;
        await fixture.Database.SaveChangesAsync(cancellationToken);
        var accountWithoutPassword = await fixture.Service.ChangePasswordAsync(
            new PasswordChangeRequest(
                fixture.User.Id,
                session.Id,
                fixture.User.Version,
                Fixture.Password,
                "Eine ausreichend lange neue Passphrase"),
            cancellationToken);
        var reauthenticationWithoutPassword = await fixture.Service.ReauthenticateAsync(
            new ReauthenticationRequest(fixture.User.Id, session.Id, Fixture.Password),
            cancellationToken);

        Assert.Equal(PasswordChangeOutcome.InvalidCurrentPassword, missingPasswordChange);
        Assert.Equal(ReauthenticationOutcome.InvalidCredentials, missingReauthentication);
        Assert.Equal(ReauthenticationOutcome.InvalidCredentials, expiredSession);
        Assert.Equal(PasswordChangeOutcome.InvalidCurrentPassword, accountWithoutPassword);
        Assert.Equal(ReauthenticationOutcome.InvalidCredentials, reauthenticationWithoutPassword);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public const string Password = "Eine sichere Testpassphrase";
        private readonly SqliteConnection connection;

        private Fixture(
            SqliteConnection connection,
            IdentityDbContext database,
            ApplicationUser user,
            PasswordHasher<ApplicationUser> hasher,
            CapturingSender sender,
            ManualTimeProvider clock,
            DateTimeOffset now)
        {
            this.connection = connection;
            Database = database;
            User = user;
            Hasher = hasher;
            Sender = sender;
            Clock = clock;
            Now = now;
            Service = new PasswordMaintenanceService(
                database,
                hasher,
                sender,
                clock,
                Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        }

        public IdentityDbContext Database { get; }
        public ApplicationUser User { get; }
        public PasswordHasher<ApplicationUser> Hasher { get; }
        public CapturingSender Sender { get; }
        public ManualTimeProvider Clock { get; }
        public DateTimeOffset Now { get; }
        public PasswordMaintenanceService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            var database = new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(cancellationToken);
            var hasher = new PasswordHasher<ApplicationUser>();
            var user = new ApplicationUser
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000011"),
                UserName = "user@example.test",
                NormalizedUserName = "USER@EXAMPLE.TEST",
                Email = "user@example.test",
                NormalizedEmail = "USER@EXAMPLE.TEST",
                EmailConfirmed = true,
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                SecurityStamp = "security-stamp",
                LockoutEnabled = true
            };
            user.PasswordHash = hasher.HashPassword(user, Password);
            database.Users.Add(user);
            await database.SaveChangesAsync(cancellationToken);
            var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
            return new Fixture(
                connection,
                database,
                user,
                hasher,
                new CapturingSender(),
                new ManualTimeProvider(now),
                now);
        }

        public LoginSessionEntity AddSession()
        {
            var session = new LoginSessionEntity
            {
                Id = Guid.NewGuid(),
                UserId = User.Id,
                CreatedAt = Now,
                ExpiresAt = Now.AddHours(12),
                IpAddress = "192.0.2.1",
                RefreshTokenHash = "hash",
                ReauthenticatedAt = Now
            };
            Database.LoginSessions.Add(session);
            return session;
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class CapturingSender : IPasswordResetSender
    {
        public List<ResetMessage> Messages { get; } = [];

        public Task SendAsync(
            string email,
            string token,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Messages.Add(new ResetMessage(email, token, expiresAt));
            return Task.CompletedTask;
        }
    }

    private sealed record ResetMessage(string Email, string Token, DateTimeOffset ExpiresAt);

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}

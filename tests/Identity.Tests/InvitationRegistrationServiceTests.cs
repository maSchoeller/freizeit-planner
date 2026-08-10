using Identity.Contracts;
using Identity.Implementation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class InvitationRegistrationServiceTests
{
    public static TheoryData<string, string, string, string, string> InvalidRegistrations => new()
    {
        { "keine-adresse", "Eine sichere Registrierungs-Passphrase", "Eine sichere Registrierungs-Passphrase", "Neue", "Person" },
        { "person@example.test", "Eine sichere Registrierungs-Passphrase", "Eine sichere Registrierungs-Passphrase", "", "Person" },
        { "person@example.test", "Eine sichere Registrierungs-Passphrase", "Eine sichere Registrierungs-Passphrase", "Neue", "" },
        { "person@example.test", "Eine sichere Registrierungs-Passphrase", "Anderes sicheres Registrierungspasswort", "Neue", "Person" },
        { "person@example.test", "zu kurz", "zu kurz", "Neue", "Person" },
        { "person@example.test", new string('x', 129), new string('x', 129), "Neue", "Person" }
    };

    [Fact]
    public async Task NewAccountConfirmsEmailConsumesInvitationAndStartsSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var invitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(
                fixture.SuperAdminId,
                InvitationGrant.SuperAdmin(),
                "192.0.2.1"),
            cancellationToken);

        var started = await fixture.Registration.BeginAsync(
            new InvitationRegistrationRequest(
                invitation.Token,
                " neue.person@example.test ",
                "Eine sichere Registrierungs-Passphrase",
                "Eine sichere Registrierungs-Passphrase",
                "Neue",
                "Person",
                "192.0.2.2"),
            cancellationToken);
        var confirmation = Assert.Single(fixture.Sender.Confirmations);
        var confirmed = await fixture.Registration.ConfirmAsync(
            new InvitationEmailConfirmation(confirmation.Token, "192.0.2.2"),
            cancellationToken);
        var reused = await fixture.Registration.ConfirmAsync(
            new InvitationEmailConfirmation(confirmation.Token, "192.0.2.2"),
            cancellationToken);

        Assert.Equal(InvitationRegistrationOutcome.ConfirmationRequired, started);
        Assert.Equal(InvitationConfirmationOutcome.Succeeded, confirmed.Outcome);
        Assert.True(confirmed.Grant?.IsSuperAdmin);
        Assert.NotNull(confirmed.Authentication?.Access.AccessToken);
        Assert.Equal(InvitationConfirmationOutcome.Used, reused.Outcome);
        Assert.Equal(
            InvitationLinkStatus.Used,
            (await fixture.Links.PreviewAsync(invitation.Token, cancellationToken))?.Status);
    }

    [Fact]
    public async Task ReservationBlocksAnotherAccountAndRestartInvalidatesPriorConfirmation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var invitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(fixture.SuperAdminId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var first = RegistrationRequest(invitation.Token, "erste@example.test");

        var started = await fixture.Registration.BeginAsync(first, cancellationToken);
        var firstConfirmation = Assert.Single(fixture.Sender.Confirmations).Token;
        var blocked = await fixture.Registration.BeginAsync(
            RegistrationRequest(invitation.Token, "andere@example.test"),
            cancellationToken);
        var restarted = await fixture.Registration.BeginAsync(first, cancellationToken);
        var stale = await fixture.Registration.ConfirmAsync(
            new InvitationEmailConfirmation(firstConfirmation, "192.0.2.2"),
            cancellationToken);

        Assert.Equal(InvitationRegistrationOutcome.ConfirmationRequired, started);
        Assert.Equal(InvitationRegistrationOutcome.Reserved, blocked);
        Assert.Equal(InvitationRegistrationOutcome.ConfirmationRequired, restarted);
        Assert.Equal(2, fixture.Sender.Confirmations.Count);
        Assert.Equal(InvitationConfirmationOutcome.Used, stale.Outcome);
    }

    [Fact]
    public async Task RegistrationParticipatesInTheRequestTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        await using var transaction = await fixture.Database.Database.BeginTransactionAsync(cancellationToken);
        var invitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(fixture.SuperAdminId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);

        var outcome = await fixture.Registration.BeginAsync(
            RegistrationRequest(invitation.Token, "transaktion@example.test"),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        Assert.Equal(InvitationRegistrationOutcome.ConfirmationRequired, outcome);
        Assert.Single(fixture.Sender.Confirmations);
    }

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task RegistrationRejectsInvalidIdentityInput(
        string email,
        string password,
        string confirmation,
        string firstName,
        string lastName)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);

        var outcome = await fixture.Registration.BeginAsync(
            new InvitationRegistrationRequest(
                new string('A', 64),
                email,
                password,
                confirmation,
                firstName,
                lastName,
                "192.0.2.2"),
            cancellationToken);

        Assert.Equal(InvitationRegistrationOutcome.InvalidInput, outcome);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("revoked")]
    [InlineData("used")]
    [InlineData("expired")]
    public async Task RegistrationRejectsUnavailableInvitation(string state)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var invitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(fixture.SuperAdminId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var stored = await fixture.Database.TransferableInvitations.SingleAsync(cancellationToken);
        if (state == "revoked") stored.RevokedAt = fixture.Clock.GetUtcNow();
        if (state == "used") stored.UsedAt = fixture.Clock.GetUtcNow();
        if (state == "expired") stored.ExpiresAt = fixture.Clock.GetUtcNow();
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var request = RegistrationRequest(
            state == "malformed" ? "not-a-token" : invitation.Token,
            $"begin-{state}@example.test");
        var outcome = await fixture.Registration.BeginAsync(request, cancellationToken);

        Assert.Equal(InvitationRegistrationOutcome.InvalidInvitation, outcome);
    }

    [Fact]
    public async Task ConfirmationRejectsMalformedAndUnknownTokens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);

        var malformed = await fixture.Registration.ConfirmAsync(
            new InvitationEmailConfirmation(new string('x', 64), "192.0.2.2"),
            cancellationToken);
        var unknown = await fixture.Registration.ConfirmAsync(
            new InvitationEmailConfirmation(new string('A', 64), "192.0.2.2"),
            cancellationToken);

        Assert.Equal(InvitationConfirmationOutcome.Invalid, malformed.Outcome);
        Assert.Equal(InvitationConfirmationOutcome.Invalid, unknown.Outcome);
    }

    [Fact]
    public async Task RegistrationRequiresCryptographicallySizedPeppers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var hasher = new PasswordHasher<ApplicationUser>();
        var sender = new CapturingSender();
        var issuer = new FixedTokenIssuer();

        Assert.Throws<ArgumentException>(() => new InvitationRegistrationService(
            fixture.Database,
            hasher,
            sender,
            issuer,
            fixture.Clock,
            new byte[31],
            new byte[32]));
        Assert.Throws<ArgumentException>(() => new InvitationRegistrationService(
            fixture.Database,
            hasher,
            sender,
            issuer,
            fixture.Clock,
            new byte[32],
            new byte[31]));
    }

    [Theory]
    [InlineData("revoked", InvitationConfirmationOutcome.Revoked)]
    [InlineData("used", InvitationConfirmationOutcome.Used)]
    [InlineData("expired-reservation", InvitationConfirmationOutcome.Expired)]
    [InlineData("confirmed-user", InvitationConfirmationOutcome.Invalid)]
    public async Task ConfirmationRejectsInvalidatedRegistrationState(
        string state,
        InvitationConfirmationOutcome expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var invitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(fixture.SuperAdminId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        _ = await fixture.Registration.BeginAsync(
            RegistrationRequest(invitation.Token, $"{state}@example.test"),
            cancellationToken);
        var storedInvitation = await fixture.Database.TransferableInvitations.SingleAsync(cancellationToken);
        var registration = await fixture.Database.InvitationRegistrations.SingleAsync(cancellationToken);
        if (state == "revoked") storedInvitation.RevokedAt = fixture.Clock.GetUtcNow();
        if (state == "used") storedInvitation.UsedAt = fixture.Clock.GetUtcNow();
        if (state == "expired-reservation") storedInvitation.ReservedUntil = fixture.Clock.GetUtcNow();
        if (state == "confirmed-user")
        {
            (await fixture.Database.Users.SingleAsync(
                item => item.Id == registration.UserId,
                cancellationToken)).EmailConfirmed = true;
        }
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var result = await fixture.Registration.ConfirmAsync(
            new InvitationEmailConfirmation(fixture.Sender.Confirmations.Single().Token, "192.0.2.2"),
            cancellationToken);

        Assert.Equal(expected, result.Outcome);
    }

    [Theory]
    [InlineData("malformed", InvitationAcceptanceOutcome.Invalid)]
    [InlineData("revoked", InvitationAcceptanceOutcome.Revoked)]
    [InlineData("used", InvitationAcceptanceOutcome.Used)]
    [InlineData("expired", InvitationAcceptanceOutcome.Expired)]
    [InlineData("reserved", InvitationAcceptanceOutcome.Reserved)]
    [InlineData("suspended-user", InvitationAcceptanceOutcome.Invalid)]
    public async Task ExistingAcceptanceRejectsUnavailableLinkOrAccount(
        string state,
        InvitationAcceptanceOutcome expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var userId = await fixture.AddConfirmedUserAsync($"{state}@example.test", cancellationToken);
        var invitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(fixture.SuperAdminId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var stored = await fixture.Database.TransferableInvitations.SingleAsync(cancellationToken);
        if (state == "revoked") stored.RevokedAt = fixture.Clock.GetUtcNow();
        if (state == "used") stored.UsedAt = fixture.Clock.GetUtcNow();
        if (state == "expired") stored.ExpiresAt = fixture.Clock.GetUtcNow();
        if (state == "reserved") stored.ReservedUntil = fixture.Clock.GetUtcNow().AddMinutes(1);
        if (state == "suspended-user")
        {
            (await fixture.Database.Users.SingleAsync(item => item.Id == userId, cancellationToken)).AccountStatus =
                AccountStatus.Suspended;
        }
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var result = await fixture.Registration.AcceptExistingAsync(
            new ExistingInvitationAcceptance(
                state == "malformed" ? "not-a-token" : invitation.Token,
                userId),
            cancellationToken);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task ExistingConfirmedAccountAcceptsLinkWithoutCreatingAnotherUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var existingUserId = await fixture.AddConfirmedUserAsync("person@example.test", cancellationToken);
        var invitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(fixture.SuperAdminId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);

        var registration = await fixture.Registration.BeginAsync(
            RegistrationRequest(invitation.Token, "person@example.test"),
            cancellationToken);
        var acceptance = await fixture.Registration.AcceptExistingAsync(
            new ExistingInvitationAcceptance(invitation.Token, existingUserId),
            cancellationToken);

        Assert.Equal(InvitationRegistrationOutcome.ExistingAccount, registration);
        Assert.Equal(InvitationAcceptanceOutcome.Accepted, acceptance.Outcome);
        Assert.True((await fixture.Database.Users.SingleAsync(
            item => item.Id == existingUserId,
            cancellationToken)).IsSuperAdmin);
        Assert.Equal(2, await fixture.Database.Users.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task OrganizationAndCampGrantsAreAddedToTheSameGlobalAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var organizationId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        fixture.Database.Organizations.Add(new OrganizationEntity
        {
            Id = organizationId,
            Name = "CVJM Sonnenhöhe",
            Slug = "sonnenhoehe",
            Status = OrganizationStatus.Active
        });
        await fixture.Database.SaveChangesAsync(cancellationToken);
        var userId = await fixture.AddConfirmedUserAsync("person@example.test", cancellationToken);
        var organizationInvitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(
                fixture.SuperAdminId,
                InvitationGrant.ForOrganizationAdmin(organizationId),
                "192.0.2.1"),
            cancellationToken);
        var campInvitation = await fixture.Links.CreateAsync(
            new CreateInvitationLinkRequest(
                fixture.SuperAdminId,
                InvitationGrant.ForCamp(organizationId, campId, CampRole.Member),
                "192.0.2.1"),
            cancellationToken);

        _ = await fixture.Registration.AcceptExistingAsync(
            new ExistingInvitationAcceptance(organizationInvitation.Token, userId),
            cancellationToken);
        _ = await fixture.Registration.AcceptExistingAsync(
            new ExistingInvitationAcceptance(campInvitation.Token, userId),
            cancellationToken);

        var membership = await fixture.Database.Memberships.SingleAsync(
            item => item.OrganizationId == organizationId && item.UserId == userId,
            cancellationToken);
        var assignment = await fixture.Database.CampAssignments.SingleAsync(
            item => item.CampId == campId && item.UserId == userId,
            cancellationToken);
        Assert.Equal(TenantRole.OrganizationAdmin, membership.Role);
        Assert.Equal(TenantRole.Member, assignment.Role);
        Assert.Equal(organizationId, assignment.OrganizationId);
    }

    private static InvitationRegistrationRequest RegistrationRequest(string token, string email) => new(
        token,
        email,
        "Eine sichere Registrierungs-Passphrase",
        "Eine sichere Registrierungs-Passphrase",
        "Neue",
        "Person",
        "192.0.2.2");

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(
            SqliteConnection connection,
            IdentityDbContext database,
            Guid superAdminId,
            TransferableInvitationLinkService links,
            InvitationRegistrationService registration,
            CapturingSender sender,
            FixedTimeProvider clock)
        {
            this.connection = connection;
            Database = database;
            SuperAdminId = superAdminId;
            Links = links;
            Registration = registration;
            Sender = sender;
            Clock = clock;
        }

        public IdentityDbContext Database { get; }
        public Guid SuperAdminId { get; }
        public TransferableInvitationLinkService Links { get; }
        public InvitationRegistrationService Registration { get; }
        public CapturingSender Sender { get; }
        public FixedTimeProvider Clock { get; }

        public async Task<Guid> AddConfirmedUserAsync(string email, CancellationToken cancellationToken)
        {
            var userId = Guid.NewGuid();
            Database.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                FirstName = "Bestehende",
                LastName = "Person",
                DisplayName = "Bestehende Person",
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString("N")
            });
            await Database.SaveChangesAsync(cancellationToken);
            return userId;
        }

        public static async Task<Fixture> CreateAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            var database = new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(cancellationToken);
            var superAdminId = Guid.NewGuid();
            database.Users.Add(new ApplicationUser
            {
                Id = superAdminId,
                UserName = "admin@example.test",
                NormalizedUserName = "ADMIN@EXAMPLE.TEST",
                Email = "admin@example.test",
                NormalizedEmail = "ADMIN@EXAMPLE.TEST",
                EmailConfirmed = true,
                FirstName = "Ada",
                LastName = "Admin",
                DisplayName = "Ada Admin",
                IsSuperAdmin = true,
                SecurityStamp = "security-stamp"
            });
            await database.SaveChangesAsync(cancellationToken);
            var now = new DateTimeOffset(2026, 8, 11, 8, 0, 0, TimeSpan.Zero);
            var clock = new FixedTimeProvider(now);
            var invitationPepper = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
            var sessionPepper = Enumerable.Range(33, 32).Select(value => (byte)value).ToArray();
            var sender = new CapturingSender();
            var tokenIssuer = new FixedTokenIssuer();
            return new Fixture(
                connection,
                database,
                superAdminId,
                new TransferableInvitationLinkService(database, clock, invitationPepper),
                new InvitationRegistrationService(
                    database,
                    new PasswordHasher<ApplicationUser>(),
                    sender,
                    tokenIssuer,
                    clock,
                    invitationPepper,
                    sessionPepper),
                sender,
                clock);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class CapturingSender : IInvitationConfirmationSender
    {
        public List<Confirmation> Confirmations { get; } = [];

        public Task SendAsync(
            string email,
            string token,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Confirmations.Add(new Confirmation(email, token, expiresAt));
            return Task.CompletedTask;
        }
    }

    private sealed record Confirmation(string Email, string Token, DateTimeOffset ExpiresAt);

    private sealed class FixedTokenIssuer : IAuthenticationTokenIssuer
    {
        private int sequence;

        public AuthenticationTokenPair Issue(AuthenticationTokenRequest request)
        {
            sequence++;
            return new AuthenticationTokenPair(
                $"access.jwt.{sequence}",
                request.IssuedAt.AddMinutes(15),
                $"refresh.jwt.{sequence}");
        }
    }

    public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}

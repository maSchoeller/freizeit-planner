using Identity.Contracts;
using Identity.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class TransferableInvitationLinkServiceTests
{
    [Fact]
    public async Task SuperAdminLinkHasOneHourLifetimeAndStoresOnlyTokenHash()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var database = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var actorId = Guid.NewGuid();
        database.Users.Add(new ApplicationUser
        {
            Id = actorId,
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
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var service = new TransferableInvitationLinkService(
            database,
            new FixedTimeProvider(now),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

        var issued = await service.CreateAsync(
            new CreateInvitationLinkRequest(actorId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var preview = await service.PreviewAsync(issued.Token, cancellationToken);

        Assert.Equal(now.AddHours(1), issued.ExpiresAt);
        Assert.True(preview?.Grant.IsSuperAdmin);
        Assert.Equal(InvitationLinkStatus.Available, preview?.Status);
        var stored = await database.TransferableInvitations.SingleAsync(cancellationToken);
        Assert.DoesNotContain(issued.Token, stored.TokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrganizationAdminCreatesOrganizationAndCampLinksOnlyInOwnOrganization()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var database = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var actorId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        database.Users.Add(ActiveUser(actorId, false));
        database.Organizations.Add(new OrganizationEntity
        {
            Id = organizationId,
            Name = "CVJM Sonnenhöhe",
            Slug = "sonnenhoehe",
            Status = OrganizationStatus.Active
        });
        database.Memberships.Add(new MembershipEntity
        {
            OrganizationId = organizationId,
            UserId = actorId,
            Role = TenantRole.OrganizationAdmin,
            Status = MembershipStatus.Active,
            OrganizationRole = OrganizationRole.OrganizationAdmin
        });
        await database.SaveChangesAsync(cancellationToken);
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(database, now);

        var organization = await service.CreateAsync(
            new CreateInvitationLinkRequest(
                actorId,
                InvitationGrant.ForOrganizationAdmin(organizationId),
                "192.0.2.1"),
            cancellationToken);
        var camp = await service.CreateAsync(
            new CreateInvitationLinkRequest(
                actorId,
                InvitationGrant.ForCamp(organizationId, Guid.NewGuid(), CampRole.Viewer),
                "192.0.2.1"),
            cancellationToken);
        var forbidden = await Assert.ThrowsAsync<IdentityRuleException>(() => service.CreateAsync(
            new CreateInvitationLinkRequest(
                actorId,
                InvitationGrant.ForOrganizationAdmin(Guid.NewGuid()),
                "192.0.2.1"),
            cancellationToken));

        Assert.Equal(now.AddHours(48), organization.ExpiresAt);
        Assert.Equal(now.AddDays(7), camp.ExpiresAt);
        Assert.Equal("organization_admin_required", forbidden.ErrorCode);
    }

    [Fact]
    public async Task RotationRevokesOldLinkAndVersionConflictsAreRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var database = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var actorId = Guid.NewGuid();
        database.Users.Add(ActiveUser(actorId, true));
        await database.SaveChangesAsync(cancellationToken);
        var service = CreateService(database, new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var issued = await service.CreateAsync(
            new CreateInvitationLinkRequest(actorId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);

        var conflict = await Assert.ThrowsAsync<IdentityRuleException>(() => service.RotateAsync(
            actorId,
            issued.Id,
            issued.Version + 1,
            cancellationToken));
        var replacement = await service.RotateAsync(actorId, issued.Id, issued.Version, cancellationToken);

        Assert.Equal("version_conflict", conflict.ErrorCode);
        Assert.Equal(InvitationLinkStatus.Revoked, (await service.PreviewAsync(
            issued.Token,
            cancellationToken))?.Status);
        Assert.Equal(InvitationLinkStatus.Available, (await service.PreviewAsync(
            replacement.Token,
            cancellationToken))?.Status);
    }

    [Fact]
    public async Task PreviewDistinguishesInvalidUnknownReservedUsedExpiredAndRevokedLinks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var database = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var actorId = Guid.NewGuid();
        database.Users.Add(ActiveUser(actorId, true));
        await database.SaveChangesAsync(cancellationToken);
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(database, now);

        Assert.Null(await service.PreviewAsync("too-short", cancellationToken));
        Assert.Null(await service.PreviewAsync(new string('Z', 64), cancellationToken));
        Assert.Null(await service.PreviewAsync(new string('A', 64), cancellationToken));

        var reserved = await service.CreateAsync(
            new CreateInvitationLinkRequest(actorId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var used = await service.CreateAsync(
            new CreateInvitationLinkRequest(actorId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var expired = await service.CreateAsync(
            new CreateInvitationLinkRequest(actorId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var revoked = await service.CreateAsync(
            new CreateInvitationLinkRequest(actorId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var entities = await database.TransferableInvitations.ToDictionaryAsync(
            item => item.Id,
            cancellationToken);
        entities[reserved.Id].ReservedUntil = now.AddMinutes(30);
        entities[used.Id].UsedAt = now;
        entities[expired.Id].ExpiresAt = now;
        entities[revoked.Id].RevokedAt = now;
        await database.SaveChangesAsync(cancellationToken);

        Assert.Equal(InvitationLinkStatus.Reserved, (await service.PreviewAsync(
            reserved.Token, cancellationToken))?.Status);
        Assert.Equal(InvitationLinkStatus.Used, (await service.PreviewAsync(
            used.Token, cancellationToken))?.Status);
        Assert.Equal(InvitationLinkStatus.Expired, (await service.PreviewAsync(
            expired.Token, cancellationToken))?.Status);
        Assert.Equal(InvitationLinkStatus.Revoked, (await service.PreviewAsync(
            revoked.Token, cancellationToken))?.Status);
    }

    [Fact]
    public async Task InvalidActorsGrantsAndInvitationMutationsAreRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var database = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var superAdminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var suspendedId = Guid.NewGuid();
        var suspendedUser = ActiveUser(suspendedId, false);
        suspendedUser.AccountStatus = AccountStatus.Suspended;
        database.Users.AddRange(
            ActiveUser(superAdminId, true),
            ActiveUser(userId, false),
            suspendedUser);
        await database.SaveChangesAsync(cancellationToken);
        var service = CreateService(database, new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));

        Assert.Throws<ArgumentException>(() => new TransferableInvitationLinkService(
            database,
            TimeProvider.System,
            new byte[31]));
        var invalidGrant = await Assert.ThrowsAsync<IdentityRuleException>(() => service.CreateAsync(
            new CreateInvitationLinkRequest(
                superAdminId,
                new InvitationGrant(false, null, null, null, null, null),
                "192.0.2.1"),
            cancellationToken));
        var missingActor = await Assert.ThrowsAsync<IdentityRuleException>(() => service.CreateAsync(
            new CreateInvitationLinkRequest(Guid.NewGuid(), InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken));
        var suspendedActor = await Assert.ThrowsAsync<IdentityRuleException>(() => service.CreateAsync(
            new CreateInvitationLinkRequest(suspendedId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken));
        var regularUser = await Assert.ThrowsAsync<IdentityRuleException>(() => service.CreateAsync(
            new CreateInvitationLinkRequest(userId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken));
        var missingInvitation = await Assert.ThrowsAsync<IdentityRuleException>(() => service.RotateAsync(
            superAdminId,
            Guid.NewGuid(),
            1,
            cancellationToken));
        var issued = await service.CreateAsync(
            new CreateInvitationLinkRequest(superAdminId, InvitationGrant.SuperAdmin(), "192.0.2.1"),
            cancellationToken);
        var entity = await database.TransferableInvitations.SingleAsync(
            item => item.Id == issued.Id,
            cancellationToken);
        entity.UsedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        var usedInvitation = await Assert.ThrowsAsync<IdentityRuleException>(() => service.RotateAsync(
            superAdminId,
            issued.Id,
            issued.Version,
            cancellationToken));

        Assert.Equal("invalid_invitation_grant", invalidGrant.ErrorCode);
        Assert.Equal("actor_not_active", missingActor.ErrorCode);
        Assert.Equal("actor_not_active", suspendedActor.ErrorCode);
        Assert.Equal("superadmin_required", regularUser.ErrorCode);
        Assert.Equal("invitation_not_found", missingInvitation.ErrorCode);
        Assert.Equal("invitation_used", usedInvitation.ErrorCode);
    }

    private static TransferableInvitationLinkService CreateService(
        IdentityDbContext database,
        DateTimeOffset now) => new(
        database,
        new FixedTimeProvider(now),
        Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    private static ApplicationUser ActiveUser(Guid id, bool superAdmin) => new()
    {
        Id = id,
        UserName = $"{id:N}@example.test",
        NormalizedUserName = $"{id:N}@EXAMPLE.TEST".ToUpperInvariant(),
        Email = $"{id:N}@example.test",
        NormalizedEmail = $"{id:N}@EXAMPLE.TEST".ToUpperInvariant(),
        EmailConfirmed = true,
        FirstName = "Ada",
        LastName = "Admin",
        DisplayName = "Ada Admin",
        IsSuperAdmin = superAdmin,
        SecurityStamp = "security-stamp"
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

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
            Role = TenantRole.OrganizationAdmin
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

using Identity.Contracts;
using Identity.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class IdentitySeederTests
{
    [Fact]
    public async Task ProductionRemainsEmptyForFirstLoginAndDevelopmentDataIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);

        await IdentitySeeder.SeedAsync(database, false, cancellationToken);
        Assert.Empty(database.Users);

        await IdentitySeeder.SeedAsync(database, true, cancellationToken);
        var lifecycle = new EfIdentityLifecycleState(database);
        var organizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Assert.Equal(6, await database.Users.CountAsync(cancellationToken));
        Assert.Equal(5, (await lifecycle.ListOrganizationMembershipsAsync(
            organizationId,
            cancellationToken)).Count);
        Assert.Equal(OrganizationStatus.Active, Assert.Single(
            await lifecycle.ListOrganizationsAsync(cancellationToken)).Status);

        await IdentitySeeder.SeedAsync(database, true, cancellationToken);
        Assert.Equal(6, await database.Users.CountAsync(cancellationToken));
        Assert.Equal(5, (await lifecycle.ListOrganizationMembershipsAsync(
            organizationId,
            cancellationToken)).Count);
    }
}

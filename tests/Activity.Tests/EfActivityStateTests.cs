using Activity.Contracts;
using Activity.Implementation;
using Identity.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Activity.Tests;

public sealed class EfActivityStateTests
{
    [Fact]
    public async Task RelationalAdapterPersistsJournalAndSearchProjectionLifecycles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<ActivityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new ActivityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var service = new ActivityService(database, new AllowActivityAccess());
        var actorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var organizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var campId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var objectId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var timestamp = new DateTimeOffset(2027, 8, 2, 10, 0, 0, TimeSpan.Zero);

        var activity = await service.RecordAsync(
            new RecordActivity(actorId, organizationId, campId, ActivityKind.Created,
                "Note", objectId, "Packliste", timestamp),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(activity.Id, Assert.Single(await service.ListAsync(
            new ActivityQuery(actorId, organizationId, campId, [ActivityKind.Created], ["Note"]),
            cancellationToken)).Id);

        var inserted = await service.UpsertAsync(
            new UpsertSearchDocument(actorId, organizationId, campId, "Note", objectId,
                "Packliste", "Bibeln und Namensschilder", new Dictionary<string, string>
                {
                    ["tag"] = "Leitung"
                }, 1, timestamp),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.True(inserted.Applied);
        Assert.Equal(objectId, Assert.Single(await service.SearchAsync(
            new CampSearchQuery(actorId, organizationId, campId, "Namensschilder", ["Note"],
                new Dictionary<string, string> { ["tag"] = "Leitung" }),
            cancellationToken)).ObjectId);

        var updated = await service.UpsertAsync(
            new UpsertSearchDocument(actorId, organizationId, campId, "Note", objectId,
                "Aktualisierte Packliste", "Bälle und Bibeln", new Dictionary<string, string>(),
                2, timestamp.AddMinutes(1)),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(inserted.Version + 1, updated.Version);
        var removed = await service.RemoveAsync(
            new RemoveSearchDocument(actorId, organizationId, campId, "Note", objectId,
                3, timestamp.AddMinutes(2)),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.True(removed.IsRemoved);
        Assert.Empty(await service.SearchAsync(
            new CampSearchQuery(actorId, organizationId, campId, string.Empty),
            cancellationToken));

        var erasure = new ActivityDataErasure(database);
        var pseudonymized = await erasure.PseudonymizeUserAsync(actorId, Guid.Empty, 50, cancellationToken);
        Assert.Equal(1, pseudonymized.ChangedRecords);
        Assert.False(pseudonymized.HasRemaining);
        var erased = await erasure.EraseOrganizationAsync(organizationId, 50, cancellationToken);
        Assert.Equal(2, erased.ChangedRecords);
        Assert.False(erased.HasRemaining);
        Assert.Equal("activity", erasure.Area);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            erasure.EraseOrganizationAsync(organizationId, 0, cancellationToken));
    }
}

using Identity.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Spiritual.Contracts;
using Spiritual.Implementation;
using Xunit;

namespace Spiritual.Tests;

public sealed class EfDevotionStateTests
{
    [Fact]
    public async Task RelationalAdapterPersistsUpdateSnapshotTrashAndRestore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<SpiritualDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new SpiritualDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var now = new DateTimeOffset(2027, 8, 2, 10, 0, 0, TimeSpan.Zero);
        var service = new DevotionPlanningService(
            new EfDevotionState(database),
            new PermitAccess(),
            new ActiveCampContext(),
            new PassageProvider(now),
            new FixedClock(now));
        var actorId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        var organizationId = Guid.Parse("72000000-0000-0000-0000-000000000001");
        var campId = Guid.Parse("73000000-0000-0000-0000-000000000001");

        var devotion = await service.CreateAsync(
            new CreateDevotion(actorId, organizationId, campId, "Gottes Liebe", "Johannes 3,16",
                BibleTranslation.Schlachter1951, "Gottes Liebe gilt allen.", "# Einstieg",
                [actorId], "Kerze und Bibeln", null),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListAsync(
            new DevotionScope(actorId, organizationId, campId), cancellationToken));
        Assert.Equal(devotion.Id, (await service.GetAsync(
            new DevotionKey(actorId, organizationId, campId, devotion.Id), cancellationToken))?.Id);

        var refreshed = await service.RefreshBibleSnapshotAsync(
            new RefreshBibleSnapshot(actorId, organizationId, campId, devotion.Id, devotion.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.NotNull(refreshed.Devotion.BibleSnapshot);
        var updated = await service.UpdateAsync(
            new UpdateDevotion(actorId, organizationId, campId, devotion.Id, "Der gute Hirte", "Psalm 23,1",
                BibleTranslation.Luther1912, devotion.CoreMessage, devotion.MarkdownContent,
                devotion.ResponsibleUserIds, devotion.MaterialNotes, null, refreshed.Devotion.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        await service.MoveToTrashAsync(
            new ChangeDevotionLifecycle(actorId, organizationId, campId, devotion.Id, updated.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        var trashed = Assert.Single(await service.ListTrashAsync(
            new DevotionScope(actorId, organizationId, campId), cancellationToken));
        await service.RestoreAsync(
            new ChangeDevotionLifecycle(actorId, organizationId, campId, devotion.Id, trashed.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.NotNull(await service.GetAsync(
            new DevotionKey(actorId, organizationId, campId, devotion.Id), cancellationToken));

        var erasure = new SpiritualDataErasure(database);
        var pseudonymized = await erasure.PseudonymizeUserAsync(actorId, Guid.Empty, 50, cancellationToken);
        Assert.Equal(1, pseudonymized.ChangedRecords);
        Assert.False(pseudonymized.HasRemaining);
        var erased = await erasure.EraseOrganizationAsync(organizationId, 50, cancellationToken);
        Assert.True(erased.ChangedRecords >= 1);
        Assert.False(erased.HasRemaining);
        Assert.Equal("spiritual", erasure.Area);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            erasure.PseudonymizeUserAsync(actorId, Guid.Empty, 501, cancellationToken));
    }

    private sealed class PermitAccess : ITenantAccessControl
    {
        public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
            OrganizationAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(TenantAccessDecision.Permit(TenantRole.OrganizationAdmin));

        public Task<TenantAccessDecision> AuthorizeCampAsync(
            CampAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(TenantAccessDecision.Permit(TenantRole.OrganizationAdmin));
    }

    private sealed class ActiveCampContext : IDevotionCampContext
    {
        public Task<DevotionCampContext> GetAsync(
            DevotionCampContextRequest request,
            CancellationToken cancellationToken) => Task.FromResult(new DevotionCampContext(false));

        public Task<bool> IsScheduleEntryWritableAsync(
            DevotionScheduleReference request,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class PassageProvider(DateTimeOffset now) : IBiblePassageProvider
    {
        public Task<BiblePassageFetchResult> FetchAsync(
            BiblePassageRequest request,
            CancellationToken cancellationToken) => Task.FromResult(BiblePassageFetchResult.Found(
            new BiblePassage(request.Reference, "Denn Gott hat die Welt so geliebt.", "deu1951",
                "Schlachter 1951", "CC BY 4.0", "Genfer Bibelgesellschaft", now)));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

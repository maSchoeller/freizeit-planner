using Activity.Contracts;
using Activity.Implementation;
using Xunit;

namespace Activity.Tests;

public sealed class ActivitySearchTests
{
    private static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CampId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task UpsertIsIdempotentAndSearchesBoundedProjection()
    {
        var subject = new ActivityService(new TestActivityState(), new AllowActivityAccess());
        var objectId = Guid.NewGuid();
        var update = new UpsertSearchDocument(
            ActorId,
            OrganizationId,
            CampId,
            "Note",
            objectId,
            "Packliste",
            "Bibeln und Namensschilder vorbereiten",
            new Dictionary<string, string> { ["tag"] = "Leitung" },
            4,
            new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero));

        var first = await subject.UpsertAsync(update, TestContext.Current.CancellationToken);
        var repeated = await subject.UpsertAsync(update, TestContext.Current.CancellationToken);
        var results = await subject.SearchAsync(
            new CampSearchQuery(
                ActorId,
                OrganizationId,
                CampId,
                "namensSCHILDER",
                ["Note"],
                new Dictionary<string, string> { ["TAG"] = "leitung" }),
            TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.True(first.Applied);
        Assert.False(repeated.Applied);
        Assert.Equal(first.Version, repeated.Version);
        Assert.Equal(objectId, result.ObjectId);
        Assert.Equal("Packliste", result.Title);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public async Task SourceVersionOrdersUpdatesRemovalAndRestoration()
    {
        var subject = new ActivityService(new TestActivityState(), new AllowActivityAccess());
        var objectId = Guid.NewGuid();
        var first = CreateUpsert(objectId, 2, "Erster Titel");

        await subject.UpsertAsync(first, TestContext.Current.CancellationToken);
        var stale = await subject.UpsertAsync(
            CreateUpsert(objectId, 1, "Veraltet"),
            TestContext.Current.CancellationToken);
        var conflict = await Assert.ThrowsAsync<ActivityRuleException>(() => subject.UpsertAsync(
            CreateUpsert(objectId, 2, "Widerspruch"),
            TestContext.Current.CancellationToken));
        var removed = await subject.RemoveAsync(
            new RemoveSearchDocument(
                ActorId,
                OrganizationId,
                CampId,
                "Note",
                objectId,
                3,
                new DateTimeOffset(2026, 8, 7, 13, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        var repeatedRemoval = await subject.RemoveAsync(
            new RemoveSearchDocument(
                ActorId,
                OrganizationId,
                CampId,
                "Note",
                objectId,
                3,
                new DateTimeOffset(2026, 8, 7, 13, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        var hidden = await subject.SearchAsync(
            new CampSearchQuery(ActorId, OrganizationId, CampId, string.Empty),
            TestContext.Current.CancellationToken);
        var restored = await subject.UpsertAsync(
            CreateUpsert(objectId, 4, "Wiederhergestellt"),
            TestContext.Current.CancellationToken);

        Assert.False(stale.Applied);
        Assert.Equal("source_version_conflict", conflict.ErrorCode);
        Assert.True(removed.Applied);
        Assert.True(removed.IsRemoved);
        Assert.False(repeatedRemoval.Applied);
        Assert.Empty(hidden);
        Assert.True(restored.Applied);
        Assert.False(restored.IsRemoved);
        Assert.Equal(3, restored.Version);
    }

    [Fact]
    public async Task SearchCannotLeakAnotherTenantProjection()
    {
        var state = new TestActivityState();
        var subject = new ActivityService(state, new AllowActivityAccess());
        var foreignCampId = Guid.Parse("30000000-0000-0000-0000-000000000099");

        await subject.UpsertAsync(CreateUpsert(Guid.NewGuid(), 1, "Eigener Treffer"), TestContext.Current.CancellationToken);
        await subject.UpsertAsync(
            CreateUpsert(Guid.NewGuid(), 1, "Fremder Treffer") with { CampId = foreignCampId },
            TestContext.Current.CancellationToken);
        var result = await subject.SearchAsync(
            new CampSearchQuery(ActorId, OrganizationId, CampId, "Treffer"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Eigener Treffer", Assert.Single(result).Title);
    }

    [Fact]
    public async Task DeniedActorCannotMutateSearchIndex()
    {
        var state = new TestActivityState();
        var subject = new ActivityService(state, new DenyActivityAccess());

        var exception = await Assert.ThrowsAsync<ActivityRuleException>(() => subject.UpsertAsync(
            CreateUpsert(Guid.NewGuid(), 1, "Nicht erlaubt"),
            TestContext.Current.CancellationToken));

        Assert.Equal("access_denied", exception.ErrorCode);
        Assert.Empty(state.SearchDocuments);
    }

    private static UpsertSearchDocument CreateUpsert(Guid objectId, long sourceVersion, string title) => new(
        ActorId,
        OrganizationId,
        CampId,
        "Note",
        objectId,
        title,
        title,
        new Dictionary<string, string>(),
        sourceVersion,
        new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero));
}

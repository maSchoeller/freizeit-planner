using Identity.Contracts;
using Spiritual.Contracts;
using Spiritual.Implementation;
using Xunit;

namespace Spiritual.Tests;

public sealed class DevotionPlanningTests
{
    [Fact]
    public async Task ExplicitRefreshStoresAnAttributedImmutableBibleSnapshot()
    {
        var state = new InMemoryDevotionState();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 9, 0, 0, TimeSpan.Zero));
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new SuccessfulBibleProvider(clock.GetUtcNow()),
            clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        var devotion = await planning.CreateAsync(CreateCommand(), cancellationToken);

        var refreshed = await planning.RefreshBibleSnapshotAsync(
            new RefreshBibleSnapshot(
                ActorId,
                OrganizationId,
                CampId,
                devotion.Id,
                devotion.Version),
            cancellationToken);

        Assert.Equal(BibleSnapshotRefreshStatus.Refreshed, refreshed.Status);
        Assert.NotNull(refreshed.Devotion.BibleSnapshot);
        Assert.Equal("deu1951", refreshed.Devotion.BibleSnapshot.TechnicalTranslationId);
        Assert.Equal("Johannes 3,16", refreshed.Devotion.BibleSnapshot.Reference);
        Assert.Equal("Denn Gott hat die Welt so geliebt.", refreshed.Devotion.BibleSnapshot.TextExcerpt);
        Assert.Equal(BibleSnapshotOrigin.Provider, refreshed.Devotion.BibleSnapshot.Origin);
        Assert.Contains("Genfer Bibelgesellschaft", refreshed.Devotion.BibleSnapshot.Attribution, StringComparison.Ordinal);
        Assert.Equal(2, refreshed.Devotion.Version);
    }

    [Fact]
    public async Task EditingAReferenceNeverSilentlyChangesTheStoredSnapshot()
    {
        var state = new InMemoryDevotionState();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 9, 0, 0, TimeSpan.Zero));
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new SuccessfulBibleProvider(clock.GetUtcNow()),
            clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        var created = await planning.CreateAsync(CreateCommand(), cancellationToken);
        var refreshed = await planning.RefreshBibleSnapshotAsync(
            new RefreshBibleSnapshot(ActorId, OrganizationId, CampId, created.Id, created.Version),
            cancellationToken);

        var updated = await planning.UpdateAsync(
            new UpdateDevotion(
                ActorId,
                OrganizationId,
                CampId,
                created.Id,
                "Der gute Hirte",
                "Psalm 23,1",
                BibleTranslation.Luther1912,
                "Gott begleitet uns.",
                "# Psalm lesen",
                [ActorId],
                string.Empty,
                null,
                refreshed.Devotion.Version),
            cancellationToken);

        Assert.Equal("Psalm 23,1", updated.BibleReference);
        Assert.Equal(BibleTranslation.Luther1912, updated.Translation);
        Assert.Equal("Johannes 3,16", updated.BibleSnapshot?.Reference);
        Assert.Equal("deu1951", updated.BibleSnapshot?.TechnicalTranslationId);
    }

    [Theory]
    [InlineData(BiblePassageFetchStatus.TimedOut, BibleSnapshotRefreshStatus.TimedOut)]
    [InlineData(BiblePassageFetchStatus.Unavailable, BibleSnapshotRefreshStatus.ProviderUnavailable)]
    public async Task ProviderFailureKeepsTheExistingSnapshotUsable(
        BiblePassageFetchStatus providerStatus,
        BibleSnapshotRefreshStatus expectedStatus)
    {
        var state = new InMemoryDevotionState();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 9, 0, 0, TimeSpan.Zero));
        var cancellationToken = TestContext.Current.CancellationToken;
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new SuccessfulBibleProvider(clock.GetUtcNow()),
            clock);
        var created = await planning.CreateAsync(CreateCommand(), cancellationToken);
        var withSnapshot = await planning.RefreshBibleSnapshotAsync(
            new RefreshBibleSnapshot(ActorId, OrganizationId, CampId, created.Id, created.Version),
            cancellationToken);
        var failedPlanning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new ResultBibleProvider(new BiblePassageFetchResult(providerStatus, null)),
            clock);

        var result = await failedPlanning.RefreshBibleSnapshotAsync(
            new RefreshBibleSnapshot(
                ActorId,
                OrganizationId,
                CampId,
                created.Id,
                withSnapshot.Devotion.Version),
            cancellationToken);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(withSnapshot.Devotion.Version, result.Devotion.Version);
        Assert.Equal(withSnapshot.Devotion.BibleSnapshot, result.Devotion.BibleSnapshot);
    }

    [Fact]
    public async Task ManualTextRemainsAvailableWhenTheProviderCannotBeReached()
    {
        var state = new InMemoryDevotionState();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 9, 0, 0, TimeSpan.Zero));
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        var created = await planning.CreateAsync(CreateCommand(), cancellationToken);
        var unavailable = await planning.RefreshBibleSnapshotAsync(
            new RefreshBibleSnapshot(ActorId, OrganizationId, CampId, created.Id, created.Version),
            cancellationToken);

        var saved = await planning.SaveManualBibleSnapshotAsync(
            new SaveManualBibleSnapshot(
                ActorId,
                OrganizationId,
                CampId,
                created.Id,
                "Johannes 3,16",
                BibleTranslation.Schlachter1951,
                "Eigener erfasster Bibeltext",
                unavailable.Devotion.Version),
            cancellationToken);

        Assert.Equal(BibleSnapshotOrigin.Manual, saved.BibleSnapshot?.Origin);
        Assert.Equal("Eigener erfasster Bibeltext", saved.BibleSnapshot?.TextExcerpt);
        Assert.Contains("Genfer Bibelgesellschaft", saved.BibleSnapshot?.Attribution, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TranslationCatalogContainsExactlyTheFourCuratedGermanTranslations()
    {
        var planning = CreatePlanning(
            new InMemoryDevotionState(),
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System);

        var translations = await planning.ListBibleTranslationsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["deu1951", "deu1912", "deuelo", "deutkw"], translations.Select(item => item.TechnicalId));
        Assert.Equal(BibleTranslation.Schlachter1951, translations.Single(item => item.IsDefault).Translation);
        Assert.Equal("Creative Commons Attribution 4.0 (CC BY 4.0)", translations[0].License);
        Assert.All(translations, item => Assert.NotEmpty(item.Attribution));
    }

    [Fact]
    public async Task ViewerCannotCreateOrChangeADevotion()
    {
        var planning = CreatePlanning(
            new InMemoryDevotionState(),
            new DenyingWriteAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<SpiritualRuleException>(() =>
            planning.CreateAsync(CreateCommand(), TestContext.Current.CancellationToken));

        Assert.Equal("camp_access_denied", exception.ErrorCode);
        Assert.Equal("Du darfst auf diese Andacht nicht zugreifen.", exception.Message);
    }

    [Fact]
    public async Task DevotionScheduleLinkMustReferenceAWritableEntry()
    {
        var state = new InMemoryDevotionState();
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System,
            new FixedDevotionCampContext(false, false));
        var command = CreateCommand() with
        {
            ScheduleEntryId = Guid.Parse("74000000-0000-0000-0000-000000000099")
        };

        var exception = await Assert.ThrowsAsync<SpiritualRuleException>(() => planning.CreateAsync(
            command,
            TestContext.Current.CancellationToken));
        var devotions = await planning.ListAsync(
            new DevotionScope(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken);

        Assert.Equal("schedule_entry_invalid", exception.ErrorCode);
        Assert.Empty(devotions);
    }

    [Fact]
    public async Task UpdatingDevotionCannotIntroduceAnInvalidScheduleLink()
    {
        var campContext = new FixedDevotionCampContext(false);
        var planning = CreatePlanning(
            new InMemoryDevotionState(),
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System,
            campContext);
        var created = await planning.CreateAsync(
            CreateCommand(),
            TestContext.Current.CancellationToken);
        campContext.ScheduleEntryIsWritable = false;

        var exception = await Assert.ThrowsAsync<SpiritualRuleException>(() => planning.UpdateAsync(
            new UpdateDevotion(
                ActorId,
                OrganizationId,
                CampId,
                created.Id,
                created.Topic,
                created.BibleReference,
                created.Translation,
                created.CoreMessage,
                created.MarkdownContent,
                created.ResponsibleUserIds,
                created.MaterialNotes,
                Guid.Parse("74000000-0000-0000-0000-000000000099"),
                created.Version),
            TestContext.Current.CancellationToken));
        var unchanged = await planning.GetAsync(
            new DevotionKey(ActorId, OrganizationId, CampId, created.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal("schedule_entry_invalid", exception.ErrorCode);
        Assert.Null(unchanged?.ScheduleEntryId);
    }

    [Fact]
    public async Task LinkedDevotionCanOnlyBeRestoredAfterItsScheduleEntry()
    {
        var campContext = new FixedDevotionCampContext(false);
        var planning = CreatePlanning(
            new InMemoryDevotionState(),
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System,
            campContext);
        var created = await planning.CreateAsync(
            CreateCommand() with
            {
                ScheduleEntryId = Guid.Parse("74000000-0000-0000-0000-000000000001")
            },
            TestContext.Current.CancellationToken);
        await planning.MoveToTrashAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version),
            TestContext.Current.CancellationToken);
        campContext.ScheduleEntryIsWritable = false;

        var exception = await Assert.ThrowsAsync<SpiritualRuleException>(() => planning.RestoreAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version + 1),
            TestContext.Current.CancellationToken));
        var trash = await planning.ListTrashAsync(
            new DevotionScope(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken);

        Assert.Equal("schedule_entry_invalid", exception.ErrorCode);
        Assert.Single(trash);
    }

    [Fact]
    public async Task StaleMutationIsRejectedAndTrashCanBeExplicitlyRestored()
    {
        var state = new InMemoryDevotionState();
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System);
        var cancellationToken = TestContext.Current.CancellationToken;
        var created = await planning.CreateAsync(CreateCommand(), cancellationToken);

        await planning.MoveToTrashAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version),
            cancellationToken);
        var hidden = await planning.GetAsync(
            new DevotionKey(ActorId, OrganizationId, CampId, created.Id),
            cancellationToken);
        var stale = await Assert.ThrowsAsync<SpiritualRuleException>(() =>
            planning.RestoreAsync(
                new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version),
                cancellationToken));
        await planning.RestoreAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version + 1),
            cancellationToken);
        var restored = await planning.GetAsync(
            new DevotionKey(ActorId, OrganizationId, CampId, created.Id),
            cancellationToken);

        Assert.Null(hidden);
        Assert.Equal("version_conflict", stale.ErrorCode);
        Assert.NotNull(restored);
        Assert.Equal(created.Version + 2, restored.Version);
    }

    [Fact]
    public async Task CampManagersCanListDeletedDevotionsWithDeterministicPurgeDeadline()
    {
        var state = new InMemoryDevotionState();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 9, 10, 0, 0, TimeSpan.Zero));
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            clock);
        var created = await planning.CreateAsync(CreateCommand(), TestContext.Current.CancellationToken);
        await planning.MoveToTrashAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version),
            TestContext.Current.CancellationToken);

        var trash = await planning.ListTrashAsync(
            new DevotionScope(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken);

        var item = Assert.Single(trash);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(clock.GetUtcNow(), item.DeletedAt);
        Assert.Equal(clock.GetUtcNow().AddDays(30), item.PurgeAt);
        Assert.Equal(created.Version + 1, item.Version);
    }

    [Fact]
    public async Task CampMemberCannotRestoreADevotionThroughTheDirectRouteSeam()
    {
        var state = new InMemoryDevotionState();
        var accessControl = new DenyingManageCampAccessControl();
        var planning = CreatePlanning(
            state,
            accessControl,
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System);
        var cancellationToken = TestContext.Current.CancellationToken;
        var created = await planning.CreateAsync(CreateCommand(), cancellationToken);
        await planning.MoveToTrashAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version),
            cancellationToken);

        var exception = await Assert.ThrowsAsync<SpiritualRuleException>(() => planning.RestoreAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version + 1),
            cancellationToken));

        Assert.Equal("camp_access_denied", exception.ErrorCode);
    }

    [Fact]
    public async Task RetentionPermanentlyRemovesDevotionsAtThirtyDays()
    {
        var state = new InMemoryDevotionState();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 9, 10, 0, 0, TimeSpan.Zero));
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            clock);
        var created = await planning.CreateAsync(CreateCommand(), TestContext.Current.CancellationToken);
        await planning.MoveToTrashAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version),
            TestContext.Current.CancellationToken);
        var retention = new DevotionRetentionService(state, clock);

        var beforeDeadline = await retention.PurgeExpiredDevotionsAsync(
            10,
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromDays(30));
        var atDeadline = await retention.PurgeExpiredDevotionsAsync(
            10,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, beforeDeadline.PurgedDevotions);
        Assert.Equal(1, atDeadline.PurgedDevotions);
        Assert.Null(await state.FindAsync(OrganizationId, CampId, created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ArchivedCampCannotRestoreADevotionThroughTheDirectRouteSeam()
    {
        var state = new InMemoryDevotionState();
        var planning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System);
        var cancellationToken = TestContext.Current.CancellationToken;
        var created = await planning.CreateAsync(CreateCommand(), cancellationToken);
        await planning.MoveToTrashAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version),
            cancellationToken);
        var archivedPlanning = CreatePlanning(
            state,
            new AllowingAccessControl(),
            new ResultBibleProvider(BiblePassageFetchResult.Unavailable()),
            TimeProvider.System,
            new FixedDevotionCampContext(true));

        var exception = await Assert.ThrowsAsync<SpiritualRuleException>(() => archivedPlanning.RestoreAsync(
            new ChangeDevotionLifecycle(ActorId, OrganizationId, CampId, created.Id, created.Version + 1),
            cancellationToken));

        Assert.Equal("camp_archived", exception.ErrorCode);
    }

    private static readonly Guid ActorId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid OrganizationId = Guid.Parse("72000000-0000-0000-0000-000000000001");
    private static readonly Guid CampId = Guid.Parse("73000000-0000-0000-0000-000000000001");

#pragma warning disable CA1859 // The acceptance seam is the public module interface.
    private static IDevotionPlanning CreatePlanning(
        IDevotionState state,
        ITenantAccessControl accessControl,
        IBiblePassageProvider provider,
        TimeProvider timeProvider,
        IDevotionCampContext? campContext = null) =>
        new DevotionPlanningService(
            state,
            accessControl,
            campContext ?? new FixedDevotionCampContext(false),
            provider,
            timeProvider);
#pragma warning restore CA1859

    private static CreateDevotion CreateCommand() => new(
        ActorId,
        OrganizationId,
        CampId,
        "Gottes Liebe",
        "Johannes 3,16",
        BibleTranslation.Schlachter1951,
        "Gottes Liebe gilt allen Menschen.",
        "# Einstieg\n\nGemeinsam lesen.",
        [ActorId],
        "Kerze und Bibeln",
        null);

    private sealed class AllowingAccessControl : ITenantAccessControl
    {
        public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
            OrganizationAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(TenantAccessDecision.Permit(TenantRole.Member));

        public Task<TenantAccessDecision> AuthorizeCampAsync(
            CampAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(TenantAccessDecision.Permit(TenantRole.Member));
    }

    private sealed class DenyingWriteAccessControl : ITenantAccessControl
    {
        public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
            OrganizationAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied));

        public Task<TenantAccessDecision> AuthorizeCampAsync(
            CampAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(request.Action == CampAction.Read
                ? TenantAccessDecision.Permit(TenantRole.Viewer)
                : TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied));
    }

    private sealed class DenyingManageCampAccessControl : ITenantAccessControl
    {
        public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
            OrganizationAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(TenantAccessDecision.Permit(TenantRole.Member));

        public Task<TenantAccessDecision> AuthorizeCampAsync(
            CampAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(request.Action == CampAction.ManageCamp
                ? TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied)
                : TenantAccessDecision.Permit(TenantRole.Member));
    }

    private sealed class FixedDevotionCampContext(
        bool isArchived,
        bool scheduleEntryIsWritable = true) : IDevotionCampContext
    {
        public bool ScheduleEntryIsWritable { get; set; } = scheduleEntryIsWritable;

        public Task<DevotionCampContext> GetAsync(
            DevotionCampContextRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DevotionCampContext(isArchived));

        public Task<bool> IsScheduleEntryWritableAsync(
            DevotionScheduleReference request,
            CancellationToken cancellationToken) => Task.FromResult(ScheduleEntryIsWritable);
    }

    private sealed class SuccessfulBibleProvider(DateTimeOffset retrievedAt) : IBiblePassageProvider
    {
        public Task<BiblePassageFetchResult> FetchAsync(
            BiblePassageRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(BiblePassageFetchResult.Found(new BiblePassage(
                request.Reference,
                "Denn Gott hat die Welt so geliebt.",
                "deu1951",
                "Schlachter 1951",
                "Creative Commons Attribution 4.0",
                "© 1951 Genfer Bibelgesellschaft; bereitgestellt durch eBible.org und Free Use Bible API.",
                retrievedAt)));
    }

    private sealed class ResultBibleProvider(BiblePassageFetchResult result) : IBiblePassageProvider
    {
        public Task<BiblePassageFetchResult> FetchAsync(
            BiblePassageRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class InMemoryDevotionState : IDevotionState
    {
        private readonly List<DevotionRecord> devotions = [];

        public ValueTask<IReadOnlyList<DevotionRecord>> ListAsync(
            Guid organizationId,
            Guid campId,
            bool includeDeleted,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DevotionRecord>>(
                devotions.Where(item => item.OrganizationId == organizationId
                    && item.CampId == campId
                    && (includeDeleted || item.DeletedAt is null)).ToArray());

        public ValueTask<DevotionRecord?> FindAsync(
            Guid organizationId,
            Guid campId,
            Guid devotionId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(devotions.SingleOrDefault(item => item.Id == devotionId));

        public ValueTask AddAsync(DevotionRecord devotion, CancellationToken cancellationToken)
        {
            devotions.Add(devotion);
            return ValueTask.CompletedTask;
        }

        public ValueTask SaveAsync(DevotionRecord devotion, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<int> PurgeDueAsync(
            DateTimeOffset cutoff,
            int batchSize,
            CancellationToken cancellationToken)
        {
            var due = devotions
                .Where(item => item.DeletedAt is not null && item.DeletedAt <= cutoff)
                .OrderBy(item => item.DeletedAt)
                .Take(batchSize)
                .ToArray();
            var removed = devotions.RemoveAll(item => due.Contains(item));
            return ValueTask.FromResult(removed);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}

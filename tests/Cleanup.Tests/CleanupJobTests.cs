using Files.Contracts;
using FreizeitCockpit.Cleanup;
using Identity.Contracts;
using Knowledge.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Spiritual.Contracts;
using Xunit;

namespace Cleanup.Tests;

public sealed class CleanupJobTests
{
    [Fact]
    public async Task RunPurgesAllBoundedRetentionAreas()
    {
        var identity = new RecordingIdentityMaintenance();
        var notes = new RecordingNotebookRetention();
        var attachments = new RecordingAttachmentMaintenance();
        var devotions = new RecordingDevotionRetention();
        var job = new CleanupJob(
            identity,
            notes,
            attachments,
            devotions,
            [],
            NullLogger<CleanupJob>.Instance,
            new CleanupOptions { BatchSize = 37, RequiredErasureAreas = [] });

        var result = await job.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(37, identity.BatchSize);
        Assert.Equal(37, notes.BatchSize);
        Assert.Equal(37, attachments.PurgeBatchSize);
        Assert.Equal(37, attachments.GrantsBatchSize);
        Assert.Equal(37, devotions.BatchSize);
        Assert.Equal(2, result.Identity.ExpiredLoginChallenges);
        Assert.Equal(3, result.Notes.PurgedNotes);
        Assert.Equal(4, result.Attachments.MetadataPurged);
        Assert.Equal(8, result.Devotions.PurgedDevotions);
        Assert.Equal(7, result.ExpiredAttachmentReadGrants);
    }

    [Fact]
    public async Task RunFailsSoTheSchedulerRetriesBlobDeletionFailures()
    {
        var job = new CleanupJob(
            new RecordingIdentityMaintenance(),
            new RecordingNotebookRetention(),
            new RecordingAttachmentMaintenance(retryableFailures: 1),
            new RecordingDevotionRetention(),
            [],
            NullLogger<CleanupJob>.Instance,
            new CleanupOptions { RequiredErasureAreas = [] });

        var exception = await Assert.ThrowsAsync<CleanupRetryableException>(
            () => job.RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, exception.RetryableFailures);
    }

    [Fact]
    public async Task RunErasesClaimedScopesBeforeFinalizingIdentityRecords()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = new RecordingIdentityMaintenance(
            new ErasureCandidates([organizationId], [userId]));
        var area = new RecordingDataErasure();
        var job = new CleanupJob(
            identity,
            new RecordingNotebookRetention(),
            new RecordingAttachmentMaintenance(),
            new RecordingDevotionRetention(),
            [area],
            NullLogger<CleanupJob>.Instance,
            new CleanupOptions { RequiredErasureAreas = ["test"] });

        _ = await job.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal([organizationId], area.Organizations);
        Assert.Equal([userId], area.Users);
        Assert.Equal([organizationId], identity.CompletedOrganizations);
        Assert.Equal([userId], identity.CompletedUsers);
    }

    [Fact]
    public async Task RunRefusesToClaimErasuresWhenAModuleIsMissing()
    {
        var identity = new RecordingIdentityMaintenance();
        var job = new CleanupJob(
            identity,
            new RecordingNotebookRetention(),
            new RecordingAttachmentMaintenance(),
            new RecordingDevotionRetention(),
            [],
            NullLogger<CleanupJob>.Instance,
            new CleanupOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => job.RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("module set", error.Message, StringComparison.Ordinal);
        Assert.False(identity.ErasuresClaimed);
    }

    private sealed class RecordingIdentityMaintenance(
        ErasureCandidates? candidates = null) : IIdentityMaintenance
    {
        public int BatchSize { get; private set; }

        public Task<IdentityCleanupResult> CleanupExpiredAsync(
            int batchSize,
            CancellationToken cancellationToken)
        {
            BatchSize = batchSize;
            return Task.FromResult(new IdentityCleanupResult(2, 0, 0, 0, 0));
        }

        public List<Guid> CompletedOrganizations { get; } = [];

        public List<Guid> CompletedUsers { get; } = [];

        public bool ErasuresClaimed { get; private set; }

        public Task<ErasureCandidates> ClaimDueErasuresAsync(
            int batchSize,
            CancellationToken cancellationToken)
        {
            ErasuresClaimed = true;
            return Task.FromResult(candidates ?? new ErasureCandidates([], []));
        }

        public Task CompleteOrganizationErasureAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
        {
            CompletedOrganizations.Add(organizationId);
            return Task.CompletedTask;
        }

        public Task CompleteAccountErasureAsync(Guid userId, CancellationToken cancellationToken)
        {
            CompletedUsers.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDataErasure : IDataErasure
    {
        public string Area => "test";

        public List<Guid> Organizations { get; } = [];

        public List<Guid> Users { get; } = [];

        public Task<DataErasureResult> EraseOrganizationAsync(
            Guid organizationId,
            int batchSize,
            CancellationToken cancellationToken)
        {
            Organizations.Add(organizationId);
            return Task.FromResult(new DataErasureResult(1, 0, false));
        }

        public Task<DataErasureResult> PseudonymizeUserAsync(
            Guid userId,
            Guid pseudonymousUserId,
            int batchSize,
            CancellationToken cancellationToken)
        {
            Users.Add(userId);
            return Task.FromResult(new DataErasureResult(1, 0, false));
        }
    }

    private sealed class RecordingNotebookRetention : INotebookRetention
    {
        public int BatchSize { get; private set; }

        public Task<NotePurgeResult> PurgeExpiredNotesAsync(
            int batchSize,
            CancellationToken cancellationToken)
        {
            BatchSize = batchSize;
            return Task.FromResult(new NotePurgeResult(3));
        }
    }

    private sealed class RecordingAttachmentMaintenance(int retryableFailures = 0)
        : IAttachmentMaintenance
    {
        public int PurgeBatchSize { get; private set; }

        public int GrantsBatchSize { get; private set; }

        public Task<AttachmentPurgeResult> PurgeDueAsync(
            int batchSize,
            CancellationToken cancellationToken)
        {
            PurgeBatchSize = batchSize;
            return Task.FromResult(new AttachmentPurgeResult(4, 5, retryableFailures));
        }

        public Task<int> DeleteExpiredReadGrantsAsync(
            int batchSize,
            CancellationToken cancellationToken)
        {
            GrantsBatchSize = batchSize;
            return Task.FromResult(7);
        }
    }

    private sealed class RecordingDevotionRetention : IDevotionRetention
    {
        public int BatchSize { get; private set; }

        public Task<DevotionPurgeResult> PurgeExpiredDevotionsAsync(
            int batchSize,
            CancellationToken cancellationToken)
        {
            BatchSize = batchSize;
            return Task.FromResult(new DevotionPurgeResult(8));
        }
    }
}

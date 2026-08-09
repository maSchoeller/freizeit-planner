using Files.Contracts;
using FreizeitCockpit.Cleanup;
using Identity.Contracts;
using Knowledge.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
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
        var job = new CleanupJob(
            identity,
            notes,
            attachments,
            NullLogger<CleanupJob>.Instance,
            new CleanupOptions { BatchSize = 37 });

        var result = await job.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(37, identity.BatchSize);
        Assert.Equal(37, notes.BatchSize);
        Assert.Equal(37, attachments.PurgeBatchSize);
        Assert.Equal(37, attachments.GrantsBatchSize);
        Assert.Equal(2, result.Identity.ExpiredLoginChallenges);
        Assert.Equal(3, result.Notes.PurgedNotes);
        Assert.Equal(4, result.Attachments.MetadataPurged);
        Assert.Equal(7, result.ExpiredAttachmentReadGrants);
    }

    [Fact]
    public async Task RunFailsSoTheSchedulerRetriesBlobDeletionFailures()
    {
        var job = new CleanupJob(
            new RecordingIdentityMaintenance(),
            new RecordingNotebookRetention(),
            new RecordingAttachmentMaintenance(retryableFailures: 1),
            NullLogger<CleanupJob>.Instance,
            new CleanupOptions());

        var exception = await Assert.ThrowsAsync<CleanupRetryableException>(
            () => job.RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, exception.RetryableFailures);
    }

    private sealed class RecordingIdentityMaintenance : IIdentityMaintenance
    {
        public int BatchSize { get; private set; }

        public Task<IdentityCleanupResult> CleanupExpiredAsync(
            int batchSize,
            CancellationToken cancellationToken)
        {
            BatchSize = batchSize;
            return Task.FromResult(new IdentityCleanupResult(2, 0, 0, 0, 0));
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
}

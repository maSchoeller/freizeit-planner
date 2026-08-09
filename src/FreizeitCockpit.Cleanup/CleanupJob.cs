using Files.Contracts;
using Identity.Contracts;
using Knowledge.Contracts;
using Microsoft.Extensions.Logging;

namespace FreizeitCockpit.Cleanup;

public sealed class CleanupOptions
{
    public int BatchSize { get; init; } = 100;
}

public sealed record CleanupResult(
    IdentityCleanupResult Identity,
    NotePurgeResult Notes,
    AttachmentPurgeResult Attachments,
    int ExpiredAttachmentReadGrants);

public sealed class CleanupRetryableException(int retryableFailures)
    : InvalidOperationException("At least one blob could not be deleted.")
{
    public int RetryableFailures { get; } = retryableFailures;
}

public sealed class CleanupJob(
    IIdentityMaintenance identityMaintenance,
    INotebookRetention notebookRetention,
    IAttachmentMaintenance attachmentMaintenance,
    ILogger<CleanupJob> logger,
    CleanupOptions options)
{
    private static readonly Action<ILogger, int, int, int, int, int, Exception?> LogCompleted =
        LoggerMessage.Define<int, int, int, int, int>(
            LogLevel.Information,
            new EventId(1001, "CleanupCompleted"),
            "Cleanup completed: {IdentityItems} identity items, {Notes} notes, "
                + "{AttachmentMetadata} attachment records, {AttachmentBlobs} blobs, "
                + "{ReadGrants} read grants.");

    public async Task<CleanupResult> RunAsync(CancellationToken cancellationToken)
    {
        if (options.BatchSize is < 1 or > 500)
        {
            throw new InvalidOperationException("Cleanup:BatchSize must be between 1 and 500.");
        }

        var identity = await identityMaintenance.CleanupExpiredAsync(
            options.BatchSize,
            cancellationToken);
        var notes = await notebookRetention.PurgeExpiredNotesAsync(
            options.BatchSize,
            cancellationToken);
        var expiredReadGrants = await attachmentMaintenance.DeleteExpiredReadGrantsAsync(
            options.BatchSize,
            cancellationToken);
        var attachments = await attachmentMaintenance.PurgeDueAsync(
            options.BatchSize,
            cancellationToken);

        LogCompleted(
            logger,
            identity.ExpiredLoginChallenges
                + identity.ExpiredEmailChangeChallenges
                + identity.ExpiredInvitations
                + identity.StaleSessions
                + identity.StaleRateEvents,
            notes.PurgedNotes,
            attachments.MetadataPurged,
            attachments.BlobsDeleted,
            expiredReadGrants,
            null);

        if (attachments.RetryableFailures > 0)
        {
            throw new CleanupRetryableException(attachments.RetryableFailures);
        }

        return new CleanupResult(identity, notes, attachments, expiredReadGrants);
    }
}

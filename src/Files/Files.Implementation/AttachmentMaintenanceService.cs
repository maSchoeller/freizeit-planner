using Files.Contracts;

namespace Files.Implementation;

public sealed class AttachmentMaintenanceService(
    FilesDbContext dbContext,
    IPrivateBlobStorage blobStorage,
    TimeProvider timeProvider) : IAttachmentMaintenance
{
    private readonly EfAttachmentState state = new(dbContext);

    public async Task<AttachmentPurgeResult> PurgeDueAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var due = await state.ListDueForPurgeAsync(
            timeProvider.GetUtcNow(),
            batchSize,
            cancellationToken);
        var metadataPurged = 0;
        var blobsDeleted = 0;
        var failures = 0;
        foreach (var attachment in due)
        {
            try
            {
                if (await blobStorage.DeleteIfExistsAsync(attachment.BlobName, cancellationToken))
                {
                    blobsDeleted++;
                }

                await state.DeletePurgedAsync(attachment, cancellationToken);
                metadataPurged++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                failures++;
            }
        }

        return new AttachmentPurgeResult(metadataPurged, blobsDeleted, failures);
    }

    public Task<int> DeleteExpiredReadGrantsAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        return state.DeleteExpiredReadGrantsAsync(
            timeProvider.GetUtcNow(),
            batchSize,
            cancellationToken).AsTask();
    }
}

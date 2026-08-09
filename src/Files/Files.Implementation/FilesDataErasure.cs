using Files.Contracts;
using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Files.Implementation;

public sealed class FilesDataErasure(
    FilesDbContext dbContext,
    IPrivateBlobStorage blobStorage) : IDataErasure
{
    public string Area => "files";

    public async Task<DataErasureResult> EraseOrganizationAsync(
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var attachments = await dbContext.Attachments
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Take(Validate(batchSize))
            .ToArrayAsync(cancellationToken);
        var changed = 0;
        var failures = 0;
        foreach (var attachment in attachments)
        {
            try
            {
                _ = await blobStorage.DeleteIfExistsAsync(attachment.BlobName, cancellationToken);
                dbContext.Attachments.Remove(attachment);
                await dbContext.SaveChangesAsync(cancellationToken);
                changed++;
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

        var remaining = await dbContext.Attachments.AnyAsync(
            item => item.OrganizationId == organizationId,
            cancellationToken);
        return new DataErasureResult(changed, failures, remaining);
    }

    public async Task<DataErasureResult> PseudonymizeUserAsync(
        Guid userId,
        Guid pseudonymousUserId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var limit = Validate(batchSize);
        var grants = await dbContext.ReadGrants
            .Where(item => item.ActorId == userId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        var attachments = await dbContext.Attachments
            .Where(item => item.CreatedBy == userId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        dbContext.ReadGrants.RemoveRange(grants);
        foreach (var attachment in attachments)
        {
            attachment.CreatedBy = pseudonymousUserId;
            attachment.Version++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.ReadGrants.AnyAsync(item => item.ActorId == userId, cancellationToken)
            || await dbContext.Attachments.AnyAsync(item => item.CreatedBy == userId, cancellationToken);
        return new DataErasureResult(grants.Length + attachments.Length, 0, remaining);
    }

    private static int Validate(int batchSize) => batchSize is >= 1 and <= 500
        ? batchSize
        : throw new ArgumentOutOfRangeException(nameof(batchSize));
}

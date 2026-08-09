using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Activity.Implementation;

public sealed class ActivityDataErasure(ActivityDbContext dbContext) : IDataErasure
{
    public string Area => "activity";

    public async Task<DataErasureResult> EraseOrganizationAsync(
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var limit = Validate(batchSize);
        var events = await dbContext.ActivityEvents
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        var documents = await dbContext.SearchDocuments
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        dbContext.ActivityEvents.RemoveRange(events);
        dbContext.SearchDocuments.RemoveRange(documents);
        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.ActivityEvents.AnyAsync(
                item => item.OrganizationId == organizationId,
                cancellationToken)
            || await dbContext.SearchDocuments.AnyAsync(
                item => item.OrganizationId == organizationId,
                cancellationToken);
        return new DataErasureResult(events.Length + documents.Length, 0, remaining);
    }

    public async Task<DataErasureResult> PseudonymizeUserAsync(
        Guid userId,
        Guid pseudonymousUserId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var events = await dbContext.ActivityEvents
            .Where(item => item.ActorId == userId)
            .OrderBy(item => item.Id)
            .Take(Validate(batchSize))
            .ToArrayAsync(cancellationToken);
        foreach (var item in events)
        {
            item.ActorId = pseudonymousUserId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.ActivityEvents.AnyAsync(
            item => item.ActorId == userId,
            cancellationToken);
        return new DataErasureResult(events.Length, 0, remaining);
    }

    private static int Validate(int batchSize) => batchSize is >= 1 and <= 500
        ? batchSize
        : throw new ArgumentOutOfRangeException(nameof(batchSize));
}

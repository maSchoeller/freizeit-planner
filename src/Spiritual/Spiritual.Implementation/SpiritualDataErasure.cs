using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Spiritual.Implementation;

public sealed class SpiritualDataErasure(SpiritualDbContext dbContext) : IDataErasure
{
    public string Area => "spiritual";

    public async Task<DataErasureResult> EraseOrganizationAsync(
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var limit = Validate(batchSize);
        var devotions = await dbContext.Devotions
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        dbContext.Devotions.RemoveRange(devotions);
        await dbContext.SaveChangesAsync(cancellationToken);

        var snapshots = await dbContext.BibleSnapshots
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        dbContext.BibleSnapshots.RemoveRange(snapshots);
        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.Devotions.AnyAsync(
                item => item.OrganizationId == organizationId,
                cancellationToken)
            || await dbContext.BibleSnapshots.AnyAsync(
                item => item.OrganizationId == organizationId,
                cancellationToken);
        return new DataErasureResult(devotions.Length + snapshots.Length, 0, remaining);
    }

    public async Task<DataErasureResult> PseudonymizeUserAsync(
        Guid userId,
        Guid pseudonymousUserId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var devotions = await dbContext.Devotions
            .Where(item => item.ResponsibleUserIds.Contains(userId))
            .OrderBy(item => item.Id)
            .Take(Validate(batchSize))
            .ToArrayAsync(cancellationToken);
        foreach (var devotion in devotions)
        {
            devotion.ResponsibleUserIds = devotion.ResponsibleUserIds
                .Where(item => item != userId)
                .ToArray();
            devotion.Version++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.Devotions.AnyAsync(
            item => item.ResponsibleUserIds.Contains(userId),
            cancellationToken);
        return new DataErasureResult(devotions.Length, 0, remaining);
    }

    private static int Validate(int batchSize) => batchSize is >= 1 and <= 500
        ? batchSize
        : throw new ArgumentOutOfRangeException(nameof(batchSize));
}

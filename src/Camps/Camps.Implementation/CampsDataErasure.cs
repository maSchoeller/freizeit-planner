using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Camps.Implementation;

public sealed class CampsDataErasure(CampsDbContext dbContext) : IDataErasure
{
    public string Area => "camps";

    public async Task<DataErasureResult> EraseOrganizationAsync(
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var camps = await dbContext.Camps
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(Validate(batchSize))
            .ToArrayAsync(cancellationToken);
        dbContext.Camps.RemoveRange(camps);
        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.Camps.AnyAsync(
            item => item.OrganizationId == organizationId,
            cancellationToken);
        return new DataErasureResult(camps.Length, 0, remaining);
    }

    public async Task<DataErasureResult> PseudonymizeUserAsync(
        Guid userId,
        Guid pseudonymousUserId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var responsibilities = await dbContext.ScheduleResponsibilities
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.ScheduleEntryId)
            .Take(Validate(batchSize))
            .ToArrayAsync(cancellationToken);
        dbContext.ScheduleResponsibilities.RemoveRange(responsibilities);
        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.ScheduleResponsibilities.AnyAsync(
            item => item.UserId == userId,
            cancellationToken);
        return new DataErasureResult(responsibilities.Length, 0, remaining);
    }

    private static int Validate(int batchSize) => batchSize is >= 1 and <= 500
        ? batchSize
        : throw new ArgumentOutOfRangeException(nameof(batchSize));
}

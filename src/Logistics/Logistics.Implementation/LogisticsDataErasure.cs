using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Implementation;

public sealed class LogisticsDataErasure(LogisticsDbContext dbContext) : IDataErasure
{
    public string Area => "logistics";

    public async Task<DataErasureResult> EraseOrganizationAsync(
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var limit = Validate(batchSize);
        var audit = await dbContext.ShoppingCheckEvents
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        var materials = await dbContext.Materials
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        var lists = await dbContext.ShoppingLists
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        dbContext.ShoppingCheckEvents.RemoveRange(audit);
        dbContext.Materials.RemoveRange(materials);
        dbContext.ShoppingLists.RemoveRange(lists);
        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.ShoppingCheckEvents.AnyAsync(
                item => item.OrganizationId == organizationId,
                cancellationToken)
            || await dbContext.Materials.AnyAsync(
                item => item.OrganizationId == organizationId,
                cancellationToken)
            || await dbContext.ShoppingLists.AnyAsync(
                item => item.OrganizationId == organizationId,
                cancellationToken);
        return new DataErasureResult(audit.Length + materials.Length + lists.Length, 0, remaining);
    }

    public async Task<DataErasureResult> PseudonymizeUserAsync(
        Guid userId,
        Guid pseudonymousUserId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var limit = Validate(batchSize);
        var materialResponsibilities = await dbContext.MaterialResponsibilities
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.MaterialRequirementId)
            .Take(limit).ToArrayAsync(cancellationToken);
        var shoppingResponsibilities = await dbContext.ShoppingItemResponsibilities
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.ShoppingItemId)
            .Take(limit).ToArrayAsync(cancellationToken);
        var items = await dbContext.ShoppingItems
            .Where(item => item.CheckedByUserId == userId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        var audit = await dbContext.ShoppingCheckEvents
            .Where(item => item.ActorId == userId)
            .OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        dbContext.MaterialResponsibilities.RemoveRange(materialResponsibilities);
        dbContext.ShoppingItemResponsibilities.RemoveRange(shoppingResponsibilities);
        foreach (var item in items)
        {
            item.CheckedByUserId = pseudonymousUserId;
            item.Version++;
        }

        foreach (var item in audit)
        {
            item.ActorId = pseudonymousUserId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.MaterialResponsibilities.AnyAsync(item => item.UserId == userId, cancellationToken)
            || await dbContext.ShoppingItemResponsibilities.AnyAsync(item => item.UserId == userId, cancellationToken)
            || await dbContext.ShoppingItems.AnyAsync(item => item.CheckedByUserId == userId, cancellationToken)
            || await dbContext.ShoppingCheckEvents.AnyAsync(item => item.ActorId == userId, cancellationToken);
        return new DataErasureResult(
            materialResponsibilities.Length + shoppingResponsibilities.Length + items.Length + audit.Length,
            0,
            remaining);
    }

    private static int Validate(int batchSize) => batchSize is >= 1 and <= 500
        ? batchSize
        : throw new ArgumentOutOfRangeException(nameof(batchSize));
}

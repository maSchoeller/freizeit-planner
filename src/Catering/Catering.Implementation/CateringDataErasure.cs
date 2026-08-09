using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Catering.Implementation;

public sealed class CateringDataErasure(CateringDbContext dbContext) : IDataErasure
{
    public string Area => "catering";

    public async Task<DataErasureResult> EraseOrganizationAsync(
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var limit = Validate(batchSize);
        var meals = await dbContext.Meals.Where(item => item.OrganizationId == organizationId).OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        var recipes = await dbContext.Recipes.Where(item => item.OrganizationId == organizationId).OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        var ingredients = await dbContext.Ingredients.Where(item => item.OrganizationId == organizationId).OrderBy(item => item.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        dbContext.Meals.RemoveRange(meals);
        dbContext.Recipes.RemoveRange(recipes);
        dbContext.Ingredients.RemoveRange(ingredients);
        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.Meals.AnyAsync(item => item.OrganizationId == organizationId, cancellationToken)
            || await dbContext.Recipes.AnyAsync(item => item.OrganizationId == organizationId, cancellationToken)
            || await dbContext.Ingredients.AnyAsync(item => item.OrganizationId == organizationId, cancellationToken);
        return new DataErasureResult(meals.Length + recipes.Length + ingredients.Length, 0, remaining);
    }

    public Task<DataErasureResult> PseudonymizeUserAsync(
        Guid userId,
        Guid pseudonymousUserId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        _ = Validate(batchSize);
        return Task.FromResult(new DataErasureResult(0, 0, false));
    }

    private static int Validate(int batchSize) => batchSize is >= 1 and <= 500
        ? batchSize
        : throw new ArgumentOutOfRangeException(nameof(batchSize));
}

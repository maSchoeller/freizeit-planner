using Microsoft.EntityFrameworkCore;

namespace Catering.Implementation;

internal interface ICateringState
{
    Task<IReadOnlyList<IngredientEntity>> SearchIngredientsAsync(
        Guid organizationId,
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken);

    Task<IngredientEntity?> FindIngredientAsync(
        Guid organizationId,
        Guid ingredientId,
        CancellationToken cancellationToken);

    Task<IngredientEntity?> FindIngredientByNormalizedNameAsync(
        Guid organizationId,
        string normalizedName,
        CancellationToken cancellationToken);

    void AddIngredient(IngredientEntity ingredient);

    Task<IReadOnlyList<RecipeEntity>> ListRecipesAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<RecipeEntity?> FindRecipeAsync(
        Guid organizationId,
        Guid recipeId,
        CancellationToken cancellationToken);

    void AddRecipe(RecipeEntity recipe);

    Task<IReadOnlyList<MealEntity>> ListMealsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    Task<MealEntity?> FindMealAsync(
        Guid organizationId,
        Guid campId,
        Guid mealId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MealEntity>> ListDeletedMealsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    Task<MealEntity?> FindDeletedMealAsync(
        Guid organizationId,
        Guid campId,
        Guid mealId,
        CancellationToken cancellationToken);

    Task<int> PurgeDueMealsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);

    void AddMeal(MealEntity meal);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed class EfCateringState(CateringDbContext dbContext) : ICateringState
{
    public async Task<IReadOnlyList<IngredientEntity>> SearchIngredientsAsync(
        Guid organizationId,
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.Ingredients
            .Where(item =>
                item.OrganizationId == organizationId &&
                item.MergedIntoIngredientId == null &&
                item.NormalizedName.Contains(normalizedQuery))
            .OrderBy(item => item.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<IngredientEntity?> FindIngredientAsync(
        Guid organizationId,
        Guid ingredientId,
        CancellationToken cancellationToken) =>
        dbContext.Ingredients.SingleOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.Id == ingredientId,
            cancellationToken);

    public Task<IngredientEntity?> FindIngredientByNormalizedNameAsync(
        Guid organizationId,
        string normalizedName,
        CancellationToken cancellationToken) =>
        dbContext.Ingredients.SingleOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.NormalizedName == normalizedName,
            cancellationToken);

    public void AddIngredient(IngredientEntity ingredient) => dbContext.Ingredients.Add(ingredient);

    public async Task<IReadOnlyList<RecipeEntity>> ListRecipesAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await dbContext.Recipes
            .Where(item => item.OrganizationId == organizationId)
            .Include(item => item.Versions)
            .ThenInclude(item => item.Ingredients)
            .ToListAsync(cancellationToken);

    public Task<RecipeEntity?> FindRecipeAsync(
        Guid organizationId,
        Guid recipeId,
        CancellationToken cancellationToken) =>
        dbContext.Recipes
            .Include(item => item.Versions)
            .ThenInclude(item => item.Ingredients)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.Id == recipeId,
                cancellationToken);

    public void AddRecipe(RecipeEntity recipe) => dbContext.Recipes.Add(recipe);

    public async Task<IReadOnlyList<MealEntity>> ListMealsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        await dbContext.Meals
            .Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.DeletedAt == null)
            .Include(item => item.RecipeSnapshots)
            .ThenInclude(item => item.Ingredients)
            .ToListAsync(cancellationToken);

    public Task<MealEntity?> FindMealAsync(
        Guid organizationId,
        Guid campId,
        Guid mealId,
        CancellationToken cancellationToken) =>
        dbContext.Meals
            .Include(item => item.RecipeSnapshots)
            .ThenInclude(item => item.Ingredients)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId
                    && item.CampId == campId
                    && item.Id == mealId
                    && item.DeletedAt == null,
                cancellationToken);

    public async Task<IReadOnlyList<MealEntity>> ListDeletedMealsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        await dbContext.Meals
            .Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.DeletedAt != null)
            .Include(item => item.RecipeSnapshots)
            .ThenInclude(item => item.Ingredients)
            .ToListAsync(cancellationToken);

    public Task<MealEntity?> FindDeletedMealAsync(
        Guid organizationId,
        Guid campId,
        Guid mealId,
        CancellationToken cancellationToken) =>
        dbContext.Meals
            .Include(item => item.RecipeSnapshots)
            .ThenInclude(item => item.Ingredients)
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.Id == mealId
                && item.DeletedAt != null,
                cancellationToken);

    public async Task<int> PurgeDueMealsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken) =>
        await dbContext.Meals
            .Where(item => item.PurgeAt != null && item.PurgeAt <= now)
            .OrderBy(item => item.PurgeAt)
            .ThenBy(item => item.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

    public void AddMeal(MealEntity meal) => dbContext.Meals.Add(meal);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

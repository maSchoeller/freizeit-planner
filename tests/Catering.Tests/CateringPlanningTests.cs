using Catering.Contracts;
using Catering.Implementation;
using Identity.Contracts;
using Xunit;

namespace Catering.Tests;

public sealed class CateringPlanningTests
{
    private static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CampId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task IngredientNamesAreUnicodeNormalizedAndOrganizationUnique()
    {
        var subject = CreateSubject();
        var first = await subject.CreateIngredientAsync(
            new CreateIngredient(ActorId, OrganizationId, "  Crème\tFraîche  "),
            TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<CateringRuleException>(() => subject.CreateIngredientAsync(
            new CreateIngredient(ActorId, OrganizationId, "CRE\u0300ME  FRAI\u0302CHE"),
            TestContext.Current.CancellationToken));
        var found = await subject.SearchIngredientsAsync(
            new IngredientSearch(ActorId, OrganizationId, " crème   "),
            TestContext.Current.CancellationToken);

        Assert.Equal("Crème Fraîche", first.Name);
        Assert.Equal("ingredient_name_exists", exception.ErrorCode);
        Assert.Single(found);
        Assert.Equal(first.Id, found[0].Id);
    }

    [Fact]
    public async Task IngredientMergeUsesCasAndAppendsRecipeVersionWithoutChangingMealSnapshot()
    {
        var context = new TestCampContext(12, false);
        var subject = CreateSubject(context: context);
        var source = await CreateIngredientAsync(subject, "Tomatenstücke");
        var target = await CreateIngredientAsync(subject, "Tomaten");
        var recipe = await CreateRecipeAsync(subject, source.Id, 400m, MeasurementUnit.Gram, 4);
        var meal = await CreateMealAsync(subject, recipe.Id);

        var preview = await subject.PreviewIngredientMergeAsync(
            new IngredientMergeRequest(ActorId, OrganizationId, source.Id, target.Id),
            TestContext.Current.CancellationToken);
        var result = await subject.MergeIngredientsAsync(
            new MergeIngredients(ActorId, OrganizationId, source.Id, target.Id, source.Version, target.Version),
            TestContext.Current.CancellationToken);
        var revisedRecipe = await subject.GetRecipeAsync(
            new RecipeRequest(ActorId, OrganizationId, recipe.Id),
            TestContext.Current.CancellationToken);
        var unchangedMeal = await subject.GetMealAsync(
            new MealRequest(ActorId, OrganizationId, CampId, meal.Id),
            TestContext.Current.CancellationToken);

        Assert.Single(preview.AffectedRecipes);
        Assert.Contains(recipe.Id, result.RevisedRecipeIds);
        Assert.Equal(2, revisedRecipe!.CurrentVersion.Number);
        Assert.Equal(target.Id, revisedRecipe.CurrentVersion.Ingredients[0].IngredientId);
        Assert.Equal(source.Id, unchangedMeal!.RecipeSnapshots[0].Ingredients[0].IngredientId);
        Assert.True(unchangedMeal.RecipeSnapshots[0].RefreshAvailable);

        var conflict = await Assert.ThrowsAsync<CateringRuleException>(() => subject.MergeIngredientsAsync(
            new MergeIngredients(ActorId, OrganizationId, source.Id, target.Id, source.Version, target.Version),
            TestContext.Current.CancellationToken));
        Assert.Equal("concurrency_conflict", conflict.ErrorCode);
    }

    [Fact]
    public async Task RecipeRevisionNeverSilentlyChangesSnapshotAndRefreshIsExplicit()
    {
        var subject = CreateSubject();
        var ingredient = await CreateIngredientAsync(subject, "Reis");
        var recipe = await CreateRecipeAsync(subject, ingredient.Id, 100m, MeasurementUnit.Gram, 4);
        var meal = await CreateMealAsync(subject, recipe.Id);

        var revised = await subject.ReviseRecipeAsync(
            new ReviseRecipe(
                ActorId,
                OrganizationId,
                recipe.Id,
                recipe.Version,
                Recipe("Gemüsereis", 4, new RecipeIngredientInput(
                    ingredient.Id,
                    new Quantity(150m, MeasurementUnit.Gram)))),
            TestContext.Current.CancellationToken);
        var beforeRefresh = await subject.GetMealAsync(
            new MealRequest(ActorId, OrganizationId, CampId, meal.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, revised.CurrentVersion.Number);
        Assert.Equal(1, beforeRefresh!.RecipeSnapshots[0].SourceRecipeVersionNumber);
        Assert.Equal(250m, beforeRefresh.RecipeSnapshots[0].Ingredients[0].ScaledQuantity.Value);

        var refreshed = await subject.RefreshRecipeSnapshotAsync(
            new RefreshRecipeSnapshot(
                ActorId,
                OrganizationId,
                CampId,
                meal.Id,
                beforeRefresh.RecipeSnapshots[0].Id,
                beforeRefresh.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, refreshed.RecipeSnapshots[0].SourceRecipeVersionNumber);
        Assert.Equal(375m, refreshed.RecipeSnapshots[0].Ingredients[0].ScaledQuantity.Value);
    }

    [Fact]
    public async Task MealUsesCurrentCampDefaultUnlessPortionOverrideIsSet()
    {
        var context = new TestCampContext(10, false);
        var subject = CreateSubject(context: context);
        var ingredient = await CreateIngredientAsync(subject, "Kartoffeln");
        var recipe = await CreateRecipeAsync(subject, ingredient.Id, 1m, MeasurementUnit.Kilogram, 4);
        var meal = await CreateMealAsync(subject, recipe.Id);

        Assert.Equal(10, meal.EffectivePortions);
        Assert.Equal(2.5m, meal.RecipeSnapshots[0].Ingredients[0].ScaledQuantity.Value);

        context.DefaultPortions = 20;
        var inherited = await subject.GetMealAsync(
            new MealRequest(ActorId, OrganizationId, CampId, meal.Id),
            TestContext.Current.CancellationToken);
        var overridden = await subject.ReviseMealAsync(
            new ReviseMeal(ActorId, OrganizationId, CampId, meal.Id, meal.Name, 5, null, inherited!.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(20, inherited.EffectivePortions);
        Assert.Equal(5m, inherited.RecipeSnapshots[0].Ingredients[0].ScaledQuantity.Value);
        Assert.Equal(5, overridden.EffectivePortions);
        Assert.Equal(1.25m, overridden.RecipeSnapshots[0].Ingredients[0].ScaledQuantity.Value);
    }

    [Fact]
    public async Task MealTrashPreservesSnapshotsAndCanBeRestoredByAManager()
    {
        var subject = CreateSubject();
        var ingredient = await CreateIngredientAsync(subject, "Bohnen");
        var recipe = await CreateRecipeAsync(subject, ingredient.Id, 500m, MeasurementUnit.Gram, 5);
        var meal = await CreateMealAsync(subject, recipe.Id);

        await subject.MoveMealToTrashAsync(
            new DeleteMeal(ActorId, OrganizationId, CampId, meal.Id, meal.Version),
            TestContext.Current.CancellationToken);
        var active = await subject.ListMealsAsync(
            new CampCateringQuery(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken);
        var trash = await subject.ListMealTrashAsync(
            new MealTrashQuery(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken);
        var deleted = Assert.Single(trash);
        var restored = await subject.RestoreMealAsync(
            new RestoreMeal(ActorId, OrganizationId, CampId, meal.Id, deleted.Version),
            TestContext.Current.CancellationToken);

        Assert.Empty(active);
        Assert.Single(restored.RecipeSnapshots);
        Assert.Equal(meal.Version + 2, restored.Version);
    }

    [Fact]
    public async Task ArchivedCampRejectsEveryMealMutation()
    {
        var context = new TestCampContext(10, false);
        var subject = CreateSubject(context: context);
        var ingredient = await CreateIngredientAsync(subject, "Linsen");
        var recipe = await CreateRecipeAsync(subject, ingredient.Id, 500m, MeasurementUnit.Gram, 5);
        var meal = await CreateMealAsync(subject, recipe.Id);
        context.IsArchived = true;

        var create = () => subject.CreateMealAsync(
            new CreateMeal(ActorId, OrganizationId, CampId, "Abendessen", null, null, [recipe.Id]),
            TestContext.Current.CancellationToken);
        var revise = () => subject.ReviseMealAsync(
            new ReviseMeal(ActorId, OrganizationId, CampId, meal.Id, "Neu", null, null, meal.Version),
            TestContext.Current.CancellationToken);
        var add = () => subject.AddRecipeSnapshotAsync(
            new AddRecipeSnapshot(ActorId, OrganizationId, CampId, meal.Id, recipe.Id, meal.Version),
            TestContext.Current.CancellationToken);
        var remove = () => subject.RemoveRecipeSnapshotAsync(
            new RemoveRecipeSnapshot(
                ActorId,
                OrganizationId,
                CampId,
                meal.Id,
                meal.RecipeSnapshots[0].Id,
                meal.Version),
            TestContext.Current.CancellationToken);
        var refresh = () => subject.RefreshRecipeSnapshotAsync(
            new RefreshRecipeSnapshot(
                ActorId,
                OrganizationId,
                CampId,
                meal.Id,
                meal.RecipeSnapshots[0].Id,
                meal.Version),
            TestContext.Current.CancellationToken);

        foreach (var mutation in new Func<Task>[] { create, revise, add, remove, refresh })
        {
            var exception = await Assert.ThrowsAsync<CateringRuleException>(mutation);
            Assert.Equal("camp_archived", exception.ErrorCode);
        }
    }

    [Fact]
    public async Task ShoppingDraftKeepsStableSourceAndCompatibleUnits()
    {
        var subject = CreateSubject();
        var ingredient = await CreateIngredientAsync(subject, "Milch");
        var recipe = await CreateRecipeAsync(subject, ingredient.Id, 500m, MeasurementUnit.Milliliter, 5);
        var meal = await CreateMealAsync(subject, recipe.Id);

        var draft = await subject.PrepareShoppingTransferAsync(
            new MealRequest(ActorId, OrganizationId, CampId, meal.Id),
            TestContext.Current.CancellationToken);

        var line = Assert.Single(draft.Lines);
        Assert.Equal(meal.RecipeSnapshots[0].Id, line.RecipeSnapshotId);
        Assert.Equal(meal.RecipeSnapshots[0].Ingredients[0].Id, line.SnapshotIngredientId);
        Assert.Equal(QuantityDimension.Volume, line.Dimension);
        Assert.Equal([MeasurementUnit.Milliliter, MeasurementUnit.Liter], line.CompatibleUnits);
        Assert.Equal(1m, line.SuggestedQuantity.Value);
    }

    [Fact]
    public async Task DeniedTenantAccessDoesNotReachCateringState()
    {
        var state = new TestCateringState();
        var subject = new CateringService(
            state,
            new DenyAccessControl(),
            new TestCampContext(10, false),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<CateringRuleException>(() => subject.CreateIngredientAsync(
            new CreateIngredient(ActorId, OrganizationId, "Salz"),
            TestContext.Current.CancellationToken));

        Assert.Equal("access_denied", exception.ErrorCode);
        Assert.Empty(state.Ingredients);
    }

    private static CateringService CreateSubject(
        TestCateringState? state = null,
        TestCampContext? context = null) =>
        new(
            state ?? new TestCateringState(),
            new AllowAccessControl(),
            context ?? new TestCampContext(10, false),
            TimeProvider.System);

    private static Task<Ingredient> CreateIngredientAsync(CateringService subject, string name) =>
        subject.CreateIngredientAsync(
            new CreateIngredient(ActorId, OrganizationId, name),
            TestContext.Current.CancellationToken);

    private static Task<Contracts.Recipe> CreateRecipeAsync(
        CateringService subject,
        Guid ingredientId,
        decimal amount,
        MeasurementUnit unit,
        int basePortions) =>
        subject.CreateRecipeAsync(
            new CreateRecipe(
                ActorId,
                OrganizationId,
                Recipe("Gemüsereis", basePortions, new RecipeIngredientInput(
                    ingredientId,
                    new Quantity(amount, unit)))),
            TestContext.Current.CancellationToken);

    private static Task<Meal> CreateMealAsync(CateringService subject, Guid recipeId) =>
        subject.CreateMealAsync(
            new CreateMeal(ActorId, OrganizationId, CampId, "Mittagessen", null, null, [recipeId]),
            TestContext.Current.CancellationToken);

    private static RecipeContent Recipe(string name, int portions, params RecipeIngredientInput[] ingredients) =>
        new(name, "Beschreibung", "Zubereitung", portions, ingredients, ["vegetarisch"], "Hinweis", null);
}

internal sealed class TestCampContext(int defaultPortions, bool isArchived) : ICampCateringContext
{
    public int DefaultPortions { get; set; } = defaultPortions;

    public bool IsArchived { get; set; } = isArchived;

    public Task<CampCateringContext> GetAsync(
        CampCateringContextRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CampCateringContext(DefaultPortions, IsArchived));
}

internal sealed class AllowAccessControl : ITenantAccessControl
{
    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Permit(TenantRole.Owner));

    public Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Permit(TenantRole.Owner));
}

internal sealed class DenyAccessControl : ITenantAccessControl
{
    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied));

    public Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied));
}

internal sealed class TestCateringState : ICateringState
{
    public List<IngredientEntity> Ingredients { get; } = [];

    public List<RecipeEntity> Recipes { get; } = [];

    public List<MealEntity> Meals { get; } = [];

    public Task<IReadOnlyList<IngredientEntity>> SearchIngredientsAsync(
        Guid organizationId,
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IngredientEntity>>(
            Ingredients
                .Where(item =>
                    item.OrganizationId == organizationId &&
                    item.MergedIntoIngredientId == null &&
                    item.NormalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
                .OrderBy(item => item.Name)
                .Take(limit)
                .ToList());

    public Task<IngredientEntity?> FindIngredientAsync(
        Guid organizationId,
        Guid ingredientId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Ingredients.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.Id == ingredientId));

    public Task<IngredientEntity?> FindIngredientByNormalizedNameAsync(
        Guid organizationId,
        string normalizedName,
        CancellationToken cancellationToken) =>
        Task.FromResult(Ingredients.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.NormalizedName == normalizedName));

    public void AddIngredient(IngredientEntity ingredient) => Ingredients.Add(ingredient);

    public Task<IReadOnlyList<RecipeEntity>> ListRecipesAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RecipeEntity>>(
            Recipes.Where(item => item.OrganizationId == organizationId).ToList());

    public Task<RecipeEntity?> FindRecipeAsync(
        Guid organizationId,
        Guid recipeId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Recipes.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.Id == recipeId));

    public void AddRecipe(RecipeEntity recipe) => Recipes.Add(recipe);

    public Task<IReadOnlyList<MealEntity>> ListMealsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MealEntity>>(
            Meals.Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.DeletedAt is null).ToList());

    public Task<MealEntity?> FindMealAsync(
        Guid organizationId,
        Guid campId,
        Guid mealId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Meals.SingleOrDefault(item =>
            item.OrganizationId == organizationId
            && item.CampId == campId
            && item.Id == mealId
            && item.DeletedAt is null));

    public Task<IReadOnlyList<MealEntity>> ListDeletedMealsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MealEntity>>(Meals.Where(item =>
            item.OrganizationId == organizationId
            && item.CampId == campId
            && item.DeletedAt is not null).ToList());

    public Task<MealEntity?> FindDeletedMealAsync(
        Guid organizationId,
        Guid campId,
        Guid mealId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Meals.SingleOrDefault(item =>
            item.OrganizationId == organizationId
            && item.CampId == campId
            && item.Id == mealId
            && item.DeletedAt is not null));

    public Task<int> PurgeDueMealsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var due = Meals.Where(item => item.PurgeAt <= now).Take(batchSize).ToArray();
        foreach (var item in due) Meals.Remove(item);
        return Task.FromResult(due.Length);
    }

    public void AddMeal(MealEntity meal) => Meals.Add(meal);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using System.Text;
using Catering.Contracts;
using Identity.Contracts;
using Microsoft.EntityFrameworkCore;
using RecipeContract = Catering.Contracts.Recipe;

namespace Catering.Implementation;

public sealed class CateringService : IOrganizationCateringLibrary, ICampMealPlanning, IMealShoppingSource
{
    private readonly ICateringState state;
    private readonly ITenantAccessControl accessControl;
    private readonly ICampCateringContext campContext;
    private readonly TimeProvider timeProvider;

    public CateringService(
        CateringDbContext dbContext,
        ITenantAccessControl accessControl,
        ICampCateringContext campContext,
        TimeProvider timeProvider)
        : this(new EfCateringState(dbContext), accessControl, campContext, timeProvider)
    {
    }

    internal CateringService(
        ICateringState state,
        ITenantAccessControl accessControl,
        ICampCateringContext campContext,
        TimeProvider timeProvider)
    {
        this.state = state;
        this.accessControl = accessControl;
        this.campContext = campContext;
        this.timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<Ingredient>> SearchIngredientsAsync(
        IngredientSearch request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.Read,
            cancellationToken);

        var query = NormalizeKey(request.Query);
        var results = await state.SearchIngredientsAsync(
            request.OrganizationId,
            query,
            Math.Clamp(request.Limit, 1, 50),
            cancellationToken);
        return results.Select(MapIngredient).ToList();
    }

    public async Task<Ingredient> CreateIngredientAsync(
        CreateIngredient request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.ManageCamps,
            cancellationToken);

        var name = NormalizeDisplayName(request.Name, "ingredient_name_required", "Bitte eine Zutatenbezeichnung angeben.");
        var normalizedName = NormalizeKey(name);
        if (await state.FindIngredientByNormalizedNameAsync(
                request.OrganizationId,
                normalizedName,
                cancellationToken) is not null)
        {
            throw Rule("ingredient_name_exists", "Diese Zutat ist bereits vorhanden.");
        }

        var ingredient = new IngredientEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = name,
            NormalizedName = normalizedName
        };
        state.AddIngredient(ingredient);
        await SaveAsync(cancellationToken);
        return MapIngredient(ingredient);
    }

    public async Task<Ingredient> RenameIngredientAsync(
        RenameIngredient request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.ManageCamps,
            cancellationToken);

        var ingredient = await RequireIngredientAsync(
            request.OrganizationId,
            request.IngredientId,
            cancellationToken);
        EnsureVersion(ingredient.Version, request.ExpectedVersion);
        EnsureNotMerged(ingredient);

        var name = NormalizeDisplayName(request.Name, "ingredient_name_required", "Bitte eine Zutatenbezeichnung angeben.");
        var normalizedName = NormalizeKey(name);
        var duplicate = await state.FindIngredientByNormalizedNameAsync(
            request.OrganizationId,
            normalizedName,
            cancellationToken);
        if (duplicate is not null && duplicate.Id != ingredient.Id)
        {
            throw Rule("ingredient_name_exists", "Diese Zutat ist bereits vorhanden.");
        }

        ingredient.Name = name;
        ingredient.NormalizedName = normalizedName;
        ingredient.Version++;
        await ReviseRecipesForIngredientAsync(
            request.OrganizationId,
            ingredient.Id,
            ingredient.Id,
            ingredient.Name,
            cancellationToken);
        await SaveAsync(cancellationToken);
        return MapIngredient(ingredient);
    }

    public async Task<IngredientMergePreview> PreviewIngredientMergeAsync(
        IngredientMergeRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.ManageCamps,
            cancellationToken);
        EnsureDistinctIngredients(request.SourceIngredientId, request.TargetIngredientId);

        var source = await RequireIngredientAsync(
            request.OrganizationId,
            request.SourceIngredientId,
            cancellationToken);
        var target = await RequireIngredientAsync(
            request.OrganizationId,
            request.TargetIngredientId,
            cancellationToken);
        EnsureNotMerged(source);
        EnsureNotMerged(target);

        var affected = await FindRecipesUsingIngredientAsync(
            request.OrganizationId,
            source.Id,
            cancellationToken);
        return new IngredientMergePreview(
            MapIngredient(source),
            MapIngredient(target),
            affected.Select(MapRecipeSummary).ToList());
    }

    public async Task<IngredientMergeResult> MergeIngredientsAsync(
        MergeIngredients request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.ManageCamps,
            cancellationToken);
        EnsureDistinctIngredients(request.SourceIngredientId, request.TargetIngredientId);

        var source = await RequireIngredientAsync(
            request.OrganizationId,
            request.SourceIngredientId,
            cancellationToken);
        var target = await RequireIngredientAsync(
            request.OrganizationId,
            request.TargetIngredientId,
            cancellationToken);
        EnsureVersion(source.Version, request.ExpectedSourceVersion);
        EnsureVersion(target.Version, request.ExpectedTargetVersion);
        EnsureNotMerged(source);
        EnsureNotMerged(target);

        var revisedIds = await ReviseRecipesForIngredientAsync(
            request.OrganizationId,
            source.Id,
            target.Id,
            target.Name,
            cancellationToken);
        source.MergedIntoIngredientId = target.Id;
        source.Version++;
        target.Version++;
        await SaveAsync(cancellationToken);
        return new IngredientMergeResult(MapIngredient(target), revisedIds);
    }

    public async Task<IReadOnlyList<RecipeSummary>> ListRecipesAsync(
        OrganizationCateringQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.Read,
            cancellationToken);
        var recipes = await state.ListRecipesAsync(request.OrganizationId, cancellationToken);
        return recipes
            .Select(MapRecipeSummary)
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<RecipeContract?> GetRecipeAsync(
        RecipeRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.Read,
            cancellationToken);
        var recipe = await state.FindRecipeAsync(request.OrganizationId, request.RecipeId, cancellationToken);
        return recipe is null ? null : MapRecipe(recipe);
    }

    public async Task<RecipeContract> CreateRecipeAsync(
        CreateRecipe request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.ManageCamps,
            cancellationToken);

        var recipe = new RecipeEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId
        };
        recipe.Versions.Add(await CreateRecipeVersionAsync(
            request.OrganizationId,
            recipe.Id,
            1,
            request.Content,
            cancellationToken));
        state.AddRecipe(recipe);
        await SaveAsync(cancellationToken);
        return MapRecipe(recipe);
    }

    public async Task<RecipeContract> ReviseRecipeAsync(
        ReviseRecipe request,
        CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(
            request.ActorId,
            request.OrganizationId,
            OrganizationAction.ManageCamps,
            cancellationToken);
        var recipe = await RequireRecipeAsync(request.OrganizationId, request.RecipeId, cancellationToken);
        EnsureVersion(recipe.Version, request.ExpectedVersion);

        recipe.Versions.Add(await CreateRecipeVersionAsync(
            request.OrganizationId,
            recipe.Id,
            CurrentVersion(recipe).Number + 1,
            request.Content,
            cancellationToken));
        recipe.Version++;
        await SaveAsync(cancellationToken);
        return MapRecipe(recipe);
    }

    public async Task<IReadOnlyList<MealSummary>> ListMealsAsync(
        CampCateringQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.Read,
            cancellationToken);
        var context = await GetCampContextAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        var meals = await state.ListMealsAsync(request.OrganizationId, request.CampId, cancellationToken);
        var summaries = new List<MealSummary>(meals.Count);
        foreach (var meal in meals)
        {
            var mapped = await MapMealAsync(meal, context.DefaultPortions, cancellationToken);
            summaries.Add(new MealSummary(
                mapped.Id,
                mapped.OrganizationId,
                mapped.CampId,
                mapped.Name,
                mapped.EffectivePortions,
                mapped.ScheduleEntryId,
                mapped.RecipeSnapshots.Count,
                mapped.Version));
        }

        return summaries.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<Meal?> GetMealAsync(MealRequest request, CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.Read,
            cancellationToken);
        var context = await GetCampContextAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        var meal = await state.FindMealAsync(
            request.OrganizationId,
            request.CampId,
            request.MealId,
            cancellationToken);
        return meal is null ? null : await MapMealAsync(meal, context.DefaultPortions, cancellationToken);
    }

    public async Task<Meal> CreateMealAsync(CreateMeal request, CancellationToken cancellationToken)
    {
        var context = await EnsureMealWriteAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        EnsurePortions(request.PortionOverride);
        await EnsureScheduleEntryWritableAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            request.ScheduleEntryId,
            cancellationToken);
        var name = NormalizeDisplayName(request.Name, "meal_name_required", "Bitte einen Namen für die Mahlzeit angeben.");

        var meal = new MealEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            CampId = request.CampId,
            Name = name,
            PortionOverride = request.PortionOverride,
            ScheduleEntryId = request.ScheduleEntryId
        };
        foreach (var recipeId in request.RecipeIds.Distinct())
        {
            var recipe = await RequireRecipeAsync(request.OrganizationId, recipeId, cancellationToken);
            meal.RecipeSnapshots.Add(CreateSnapshot(meal, recipe));
        }

        state.AddMeal(meal);
        await SaveAsync(cancellationToken);
        return await MapMealAsync(meal, context.DefaultPortions, cancellationToken);
    }

    public async Task<Meal> ReviseMealAsync(ReviseMeal request, CancellationToken cancellationToken)
    {
        var context = await EnsureMealWriteAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        EnsurePortions(request.PortionOverride);
        await EnsureScheduleEntryWritableAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            request.ScheduleEntryId,
            cancellationToken);
        var meal = await RequireMealAsync(
            request.OrganizationId,
            request.CampId,
            request.MealId,
            cancellationToken);
        EnsureVersion(meal.Version, request.ExpectedVersion);

        meal.Name = NormalizeDisplayName(request.Name, "meal_name_required", "Bitte einen Namen für die Mahlzeit angeben.");
        meal.PortionOverride = request.PortionOverride;
        meal.ScheduleEntryId = request.ScheduleEntryId;
        meal.Version++;
        await SaveAsync(cancellationToken);
        return await MapMealAsync(meal, context.DefaultPortions, cancellationToken);
    }

    public async Task MoveMealToTrashAsync(DeleteMeal request, CancellationToken cancellationToken)
    {
        _ = await EnsureMealWriteAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        var meal = await RequireMealAsync(
            request.OrganizationId,
            request.CampId,
            request.MealId,
            cancellationToken);
        EnsureVersion(meal.Version, request.ExpectedVersion);
        var now = timeProvider.GetUtcNow();
        meal.DeletedAt = now;
        meal.PurgeAt = now.AddDays(30);
        meal.Version++;
        await SaveAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrashedMeal>> ListMealTrashAsync(
        MealTrashQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.ManageCamp,
            cancellationToken);
        return (await state.ListDeletedMealsAsync(
                request.OrganizationId,
                request.CampId,
                cancellationToken))
            .OrderByDescending(item => item.DeletedAt)
            .Select(item => new TrashedMeal(
                item.Id,
                item.OrganizationId,
                item.CampId,
                item.Name,
                item.ScheduleEntryId,
                item.DeletedAt!.Value,
                item.PurgeAt!.Value,
                item.Version))
            .ToArray();
    }

    public async Task<Meal> RestoreMealAsync(RestoreMeal request, CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.ManageCamp,
            cancellationToken);
        var context = await GetCampContextAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        if (context.IsArchived)
        {
            throw Rule("camp_archived", "Archivierte Freizeiten können nicht mehr bearbeitet werden.");
        }
        var meal = await state.FindDeletedMealAsync(
            request.OrganizationId,
            request.CampId,
            request.MealId,
            cancellationToken)
            ?? throw Rule("meal_not_found", "Die Mahlzeit wurde nicht gefunden.");
        EnsureVersion(meal.Version, request.ExpectedVersion);
        if (meal.PurgeAt is null || meal.PurgeAt <= timeProvider.GetUtcNow())
        {
            throw Rule("meal_restore_expired", "Die Aufbewahrungsfrist ist abgelaufen.");
        }
        await EnsureScheduleEntryWritableAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            meal.ScheduleEntryId,
            cancellationToken);
        meal.DeletedAt = null;
        meal.PurgeAt = null;
        meal.Version++;
        await SaveAsync(cancellationToken);
        return await MapMealAsync(meal, context.DefaultPortions, cancellationToken);
    }

    public async Task<Meal> AddRecipeSnapshotAsync(
        AddRecipeSnapshot request,
        CancellationToken cancellationToken)
    {
        var context = await EnsureMealWriteAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        var meal = await RequireMealAsync(
            request.OrganizationId,
            request.CampId,
            request.MealId,
            cancellationToken);
        EnsureVersion(meal.Version, request.ExpectedVersion);
        var recipe = await RequireRecipeAsync(request.OrganizationId, request.RecipeId, cancellationToken);

        meal.RecipeSnapshots.Add(CreateSnapshot(meal, recipe));
        meal.Version++;
        await SaveAsync(cancellationToken);
        return await MapMealAsync(meal, context.DefaultPortions, cancellationToken);
    }

    public async Task<Meal> RemoveRecipeSnapshotAsync(
        RemoveRecipeSnapshot request,
        CancellationToken cancellationToken)
    {
        var context = await EnsureMealWriteAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        var meal = await RequireMealAsync(
            request.OrganizationId,
            request.CampId,
            request.MealId,
            cancellationToken);
        EnsureVersion(meal.Version, request.ExpectedVersion);
        var snapshot = RequireCurrentSnapshot(meal, request.RecipeSnapshotId);

        snapshot.IsCurrent = false;
        meal.Version++;
        await SaveAsync(cancellationToken);
        return await MapMealAsync(meal, context.DefaultPortions, cancellationToken);
    }

    public async Task<Meal> RefreshRecipeSnapshotAsync(
        RefreshRecipeSnapshot request,
        CancellationToken cancellationToken)
    {
        var context = await EnsureMealWriteAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        var meal = await RequireMealAsync(
            request.OrganizationId,
            request.CampId,
            request.MealId,
            cancellationToken);
        EnsureVersion(meal.Version, request.ExpectedVersion);
        var snapshot = RequireCurrentSnapshot(meal, request.RecipeSnapshotId);
        var recipe = await RequireRecipeAsync(request.OrganizationId, snapshot.SourceRecipeId, cancellationToken);
        if (CurrentVersion(recipe).Number <= snapshot.SourceRecipeVersionNumber)
        {
            throw Rule("snapshot_current", "Der Rezept-Snapshot ist bereits aktuell.");
        }

        snapshot.IsCurrent = false;
        meal.RecipeSnapshots.Add(CreateSnapshot(meal, recipe));
        meal.Version++;
        await SaveAsync(cancellationToken);
        return await MapMealAsync(meal, context.DefaultPortions, cancellationToken);
    }

    public async Task<MealShoppingDraft> PrepareShoppingTransferAsync(
        MealRequest request,
        CancellationToken cancellationToken)
    {
        var meal = await GetMealAsync(request, cancellationToken) ??
            throw Rule("meal_not_found", "Die Mahlzeit wurde nicht gefunden.");
        var lines = meal.RecipeSnapshots
            .SelectMany(snapshot => snapshot.Ingredients.Select(ingredient =>
            {
                var suggested = UseReadableUnit(ingredient.ScaledQuantity);
                return new MealShoppingLine(
                    snapshot.Id,
                    ingredient.Id,
                    snapshot.SourceRecipeId,
                    snapshot.SourceRecipeVersionNumber,
                    $"{meal.Name} – {snapshot.Name}",
                    ingredient.IngredientName,
                    suggested,
                    suggested.Dimension,
                    suggested.CompatibleUnits);
            }))
            .ToList();
        return new MealShoppingDraft(meal.Id, meal.Name, meal.EffectivePortions, meal.Version, lines);
    }

    private async Task<RecipeVersionEntity> CreateRecipeVersionAsync(
        Guid organizationId,
        Guid recipeId,
        int number,
        RecipeContent content,
        CancellationToken cancellationToken)
    {
        ValidateRecipeContent(content);
        var version = new RecipeVersionEntity
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            OrganizationId = organizationId,
            Number = number,
            Name = NormalizeDisplayName(content.Name, "recipe_name_required", "Bitte einen Rezeptnamen angeben."),
            Description = NormalizeOptionalText(content.Description),
            Preparation = NormalizeOptionalText(content.Preparation),
            BasePortions = content.BasePortions,
            DietaryTags = NormalizeTags(content.DietaryTags),
            AllergenNotes = NormalizeNullableText(content.AllergenNotes),
            KitchenNotes = NormalizeNullableText(content.KitchenNotes),
            CreatedAt = timeProvider.GetUtcNow()
        };

        foreach (var input in content.Ingredients)
        {
            var ingredient = await ResolveIngredientAsync(organizationId, input.IngredientId, cancellationToken);
            version.Ingredients.Add(new RecipeIngredientEntity
            {
                Id = Guid.NewGuid(),
                RecipeVersionId = version.Id,
                OrganizationId = organizationId,
                IngredientId = ingredient.Id,
                IngredientName = ingredient.Name,
                Amount = input.Quantity.Value,
                Unit = input.Quantity.Unit,
                CountUnitName = input.Quantity.CountUnitName,
                Note = NormalizeNullableText(input.Note)
            });
        }

        return version;
    }

    private async Task<IReadOnlyList<Guid>> ReviseRecipesForIngredientAsync(
        Guid organizationId,
        Guid sourceIngredientId,
        Guid targetIngredientId,
        string targetName,
        CancellationToken cancellationToken)
    {
        var affected = await FindRecipesUsingIngredientAsync(
            organizationId,
            sourceIngredientId,
            cancellationToken);
        foreach (var recipe in affected)
        {
            var current = CurrentVersion(recipe);
            var revised = CloneRecipeVersion(recipe.Id, current, current.Number + 1, sourceIngredientId, targetIngredientId, targetName);
            recipe.Versions.Add(revised);
            recipe.Version++;
        }

        return affected.Select(item => item.Id).ToList();
    }

    private async Task<IReadOnlyList<RecipeEntity>> FindRecipesUsingIngredientAsync(
        Guid organizationId,
        Guid ingredientId,
        CancellationToken cancellationToken)
    {
        var recipes = await state.ListRecipesAsync(organizationId, cancellationToken);
        return recipes.Where(recipe => CurrentVersion(recipe).Ingredients.Any(line => line.IngredientId == ingredientId)).ToList();
    }

    private RecipeVersionEntity CloneRecipeVersion(
        Guid recipeId,
        RecipeVersionEntity source,
        int number,
        Guid sourceIngredientId,
        Guid targetIngredientId,
        string targetName)
    {
        var clone = new RecipeVersionEntity
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            OrganizationId = source.OrganizationId,
            Number = number,
            Name = source.Name,
            Description = source.Description,
            Preparation = source.Preparation,
            BasePortions = source.BasePortions,
            DietaryTags = [.. source.DietaryTags],
            AllergenNotes = source.AllergenNotes,
            KitchenNotes = source.KitchenNotes,
            CreatedAt = timeProvider.GetUtcNow()
        };
        foreach (var line in source.Ingredients)
        {
            var replace = line.IngredientId == sourceIngredientId;
            clone.Ingredients.Add(new RecipeIngredientEntity
            {
                Id = Guid.NewGuid(),
                RecipeVersionId = clone.Id,
                OrganizationId = source.OrganizationId,
                IngredientId = replace ? targetIngredientId : line.IngredientId,
                IngredientName = replace ? targetName : line.IngredientName,
                Amount = line.Amount,
                Unit = line.Unit,
                CountUnitName = line.CountUnitName,
                Note = line.Note
            });
        }

        return clone;
    }

    private RecipeSnapshotEntity CreateSnapshot(MealEntity meal, RecipeEntity recipe)
    {
        var source = CurrentVersion(recipe);
        var snapshot = new RecipeSnapshotEntity
        {
            Id = Guid.NewGuid(),
            MealId = meal.Id,
            OrganizationId = meal.OrganizationId,
            CampId = meal.CampId,
            SourceRecipeId = recipe.Id,
            SourceRecipeVersionNumber = source.Number,
            Name = source.Name,
            Description = source.Description,
            Preparation = source.Preparation,
            BasePortions = source.BasePortions,
            DietaryTags = [.. source.DietaryTags],
            AllergenNotes = source.AllergenNotes,
            KitchenNotes = source.KitchenNotes,
            CapturedAt = timeProvider.GetUtcNow()
        };
        foreach (var line in source.Ingredients)
        {
            snapshot.Ingredients.Add(new SnapshotIngredientEntity
            {
                Id = Guid.NewGuid(),
                RecipeSnapshotId = snapshot.Id,
                OrganizationId = meal.OrganizationId,
                CampId = meal.CampId,
                IngredientId = line.IngredientId,
                IngredientName = line.IngredientName,
                Amount = line.Amount,
                Unit = line.Unit,
                CountUnitName = line.CountUnitName,
                Note = line.Note
            });
        }

        return snapshot;
    }

    private async Task<Meal> MapMealAsync(
        MealEntity meal,
        int campDefaultPortions,
        CancellationToken cancellationToken)
    {
        var effectivePortions = meal.PortionOverride ?? campDefaultPortions;
        var snapshots = new List<RecipeSnapshot>();
        foreach (var snapshot in meal.RecipeSnapshots.Where(item => item.IsCurrent).OrderBy(item => item.CapturedAt))
        {
            var sourceRecipe = await state.FindRecipeAsync(
                meal.OrganizationId,
                snapshot.SourceRecipeId,
                cancellationToken);
            var latestVersion = sourceRecipe is null
                ? snapshot.SourceRecipeVersionNumber
                : CurrentVersion(sourceRecipe).Number;
            snapshots.Add(new RecipeSnapshot(
                snapshot.Id,
                snapshot.SourceRecipeId,
                snapshot.SourceRecipeVersionNumber,
                latestVersion,
                latestVersion > snapshot.SourceRecipeVersionNumber,
                snapshot.Name,
                snapshot.Description,
                snapshot.Preparation,
                snapshot.BasePortions,
                snapshot.Ingredients.Select(line => new SnapshotIngredient(
                    line.Id,
                    line.IngredientId,
                    line.IngredientName,
                    ToQuantity(line.Amount, line.Unit, line.CountUnitName),
                    ToQuantity(line.Amount * effectivePortions / snapshot.BasePortions, line.Unit, line.CountUnitName),
                    line.Note)).ToList(),
                snapshot.DietaryTags,
                snapshot.AllergenNotes,
                snapshot.KitchenNotes,
                snapshot.CapturedAt));
        }

        return new Meal(
            meal.Id,
            meal.OrganizationId,
            meal.CampId,
            meal.Name,
            campDefaultPortions,
            meal.PortionOverride,
            effectivePortions,
            meal.ScheduleEntryId,
            snapshots,
            meal.Version);
    }

    private static Ingredient MapIngredient(IngredientEntity ingredient) =>
        new(
            ingredient.Id,
            ingredient.OrganizationId,
            ingredient.Name,
            ingredient.MergedIntoIngredientId is not null,
            ingredient.MergedIntoIngredientId,
            ingredient.Version);

    private static RecipeSummary MapRecipeSummary(RecipeEntity recipe)
    {
        var current = CurrentVersion(recipe);
        return new RecipeSummary(
            recipe.Id,
            recipe.OrganizationId,
            current.Name,
            current.BasePortions,
            current.Number,
            recipe.Version);
    }

    private static RecipeContract MapRecipe(RecipeEntity recipe)
    {
        var current = CurrentVersion(recipe);
        return new RecipeContract(
            recipe.Id,
            recipe.OrganizationId,
            new RecipeVersion(
                current.Id,
                current.Number,
                current.Name,
                current.Description,
                current.Preparation,
                current.BasePortions,
                current.Ingredients.Select(line => new RecipeIngredient(
                    line.Id,
                    line.IngredientId,
                    line.IngredientName,
                    ToQuantity(line.Amount, line.Unit, line.CountUnitName),
                    line.Note)).ToList(),
                current.DietaryTags,
                current.AllergenNotes,
                current.KitchenNotes,
                current.CreatedAt),
            recipe.Version);
    }

    private async Task<IngredientEntity> ResolveIngredientAsync(
        Guid organizationId,
        Guid ingredientId,
        CancellationToken cancellationToken)
    {
        var ingredient = await RequireIngredientAsync(organizationId, ingredientId, cancellationToken);
        var visited = new HashSet<Guid>();
        while (ingredient.MergedIntoIngredientId is Guid targetId)
        {
            if (!visited.Add(ingredient.Id))
            {
                throw Rule("ingredient_merge_cycle", "Die Zusammenführung dieser Zutaten ist ungültig.");
            }

            ingredient = await RequireIngredientAsync(organizationId, targetId, cancellationToken);
        }

        return ingredient;
    }

    private async Task<IngredientEntity> RequireIngredientAsync(
        Guid organizationId,
        Guid ingredientId,
        CancellationToken cancellationToken) =>
        await state.FindIngredientAsync(organizationId, ingredientId, cancellationToken) ??
            throw Rule("ingredient_not_found", "Die Zutat wurde nicht gefunden.");

    private async Task<RecipeEntity> RequireRecipeAsync(
        Guid organizationId,
        Guid recipeId,
        CancellationToken cancellationToken) =>
        await state.FindRecipeAsync(organizationId, recipeId, cancellationToken) ??
            throw Rule("recipe_not_found", "Das Rezept wurde nicht gefunden.");

    private async Task<MealEntity> RequireMealAsync(
        Guid organizationId,
        Guid campId,
        Guid mealId,
        CancellationToken cancellationToken) =>
        await state.FindMealAsync(organizationId, campId, mealId, cancellationToken) ??
            throw Rule("meal_not_found", "Die Mahlzeit wurde nicht gefunden.");

    private static RecipeSnapshotEntity RequireCurrentSnapshot(MealEntity meal, Guid snapshotId) =>
        meal.RecipeSnapshots.SingleOrDefault(item => item.Id == snapshotId && item.IsCurrent) ??
            throw Rule("snapshot_not_found", "Der Rezept-Snapshot wurde nicht gefunden.");

    private async Task<CampCateringContext> EnsureMealWriteAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(actorId, organizationId, campId, CampAction.WriteContent, cancellationToken);
        var context = await GetCampContextAsync(actorId, organizationId, campId, cancellationToken);
        if (context.IsArchived)
        {
            throw Rule("camp_archived", "Archivierte Freizeiten können nicht mehr bearbeitet werden.");
        }

        return context;
    }

    private async Task EnsureScheduleEntryWritableAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        Guid? scheduleEntryId,
        CancellationToken cancellationToken)
    {
        if (scheduleEntryId is null) return;
        if (!await campContext.IsScheduleEntryWritableAsync(
                new CampCateringScheduleReference(actorId, organizationId, campId, scheduleEntryId.Value),
                cancellationToken))
        {
            throw Rule(
                "schedule_entry_invalid",
                "Der verknüpfte Zeitplaneintrag wurde nicht gefunden oder kann nicht bearbeitet werden.");
        }
    }

    private async Task<CampCateringContext> GetCampContextAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var context = await campContext.GetAsync(
            new CampCateringContextRequest(actorId, organizationId, campId),
            cancellationToken);
        if (context.DefaultPortions <= 0)
        {
            throw Rule("invalid_camp_portions", "Die Standard-Personenzahl der Freizeit ist ungültig.");
        }

        return context;
    }

    private async Task EnsureOrganizationAccessAsync(
        Guid actorId,
        Guid organizationId,
        OrganizationAction action,
        CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(actorId, organizationId, action),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("access_denied", "Für diese Aktion fehlt die Berechtigung.");
        }
    }

    private async Task EnsureCampAccessAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CampAction action,
        CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeCampAsync(
            new CampAccessRequest(actorId, organizationId, campId, action),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("access_denied", "Für diese Aktion fehlt die Berechtigung.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await state.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Rule("concurrency_conflict", "Der Datensatz wurde zwischenzeitlich geändert.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw Rule("ingredient_name_exists", "Diese Zutat ist bereits vorhanden.");
        }
    }

    private static void ValidateRecipeContent(RecipeContent content)
    {
        if (content.BasePortions <= 0)
        {
            throw Rule("invalid_base_portions", "Die Basisportionen müssen größer als null sein.");
        }

        if (content.Ingredients.Count == 0)
        {
            throw Rule("recipe_ingredients_required", "Bitte mindestens eine Rezeptzutat angeben.");
        }
    }

    private static void EnsurePortions(int? portions)
    {
        if (portions is <= 0)
        {
            throw Rule("invalid_portions", "Die Personenzahl muss größer als null sein.");
        }
    }

    private static void EnsureVersion(long actual, long expected)
    {
        if (actual != expected)
        {
            throw Rule("concurrency_conflict", "Der Datensatz wurde zwischenzeitlich geändert.");
        }
    }

    private static void EnsureNotMerged(IngredientEntity ingredient)
    {
        if (ingredient.MergedIntoIngredientId is not null)
        {
            throw Rule("ingredient_already_merged", "Diese Zutat wurde bereits zusammengeführt.");
        }
    }

    private static void EnsureDistinctIngredients(Guid sourceId, Guid targetId)
    {
        if (sourceId == targetId)
        {
            throw Rule("ingredient_merge_same", "Quelle und Ziel müssen unterschiedliche Zutaten sein.");
        }
    }

    private static RecipeVersionEntity CurrentVersion(RecipeEntity recipe) =>
        recipe.Versions.MaxBy(item => item.Number) ??
            throw Rule("recipe_version_missing", "Das Rezept enthält keine gültige Version.");

    private static string NormalizeDisplayName(string value, string errorCode, string message)
    {
        var normalized = CollapseWhitespace(value.Normalize(NormalizationForm.FormKC));
        if (normalized.Length == 0)
        {
            throw Rule(errorCode, message);
        }

        if (normalized.Length > 160)
        {
            throw Rule("name_too_long", "Die Bezeichnung darf höchstens 160 Zeichen lang sein.");
        }

        return normalized;
    }

    private static string NormalizeKey(string value) =>
        CollapseWhitespace(value.Normalize(NormalizationForm.FormKC)).ToUpperInvariant();

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = true;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().TrimEnd();
    }

    private static string NormalizeOptionalText(string value) => value.Trim();

    private static string? NormalizeNullableText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] NormalizeTags(IReadOnlyList<string> tags) =>
        tags.Select(CollapseWhitespace)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static Quantity ToQuantity(decimal amount, MeasurementUnit unit, string? countUnitName) =>
        new(amount, unit, countUnitName);

    private static Quantity UseReadableUnit(Quantity quantity) => quantity.Unit switch
    {
        MeasurementUnit.Gram when quantity.Value >= 1000m => quantity.ConvertTo(MeasurementUnit.Kilogram),
        MeasurementUnit.Milliliter when quantity.Value >= 1000m => quantity.ConvertTo(MeasurementUnit.Liter),
        _ => quantity
    };

    private static CateringRuleException Rule(string code, string message) => new(code, message);
}

using Activity.Contracts;
using Catering.Contracts;
using Microsoft.AspNetCore.Antiforgery;

internal static class CateringEndpoints
{
    public static IEndpointRouteBuilder MapCateringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var library = endpoints.MapGroup("/api/v1/organizations/{organizationId:guid}/catering")
            .RequireAuthorization();
        library.MapGet("/ingredients", SearchIngredientsAsync);
        library.MapPost("/ingredients", CreateIngredientAsync);
        library.MapPut("/ingredients/{ingredientId:guid}", RenameIngredientAsync);
        library.MapPost("/ingredients/merge-preview", PreviewMergeAsync);
        library.MapPost("/ingredients/merge", MergeIngredientsAsync);
        library.MapGet("/recipes", ListRecipesAsync);
        library.MapGet("/recipes/{recipeId:guid}", GetRecipeAsync);
        library.MapPost("/recipes", CreateRecipeAsync);
        library.MapPut("/recipes/{recipeId:guid}", ReviseRecipeAsync);

        var meals = endpoints.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/camps/{campId:guid}/catering/meals")
            .RequireAuthorization();
        meals.MapGet("/", ListMealsAsync);
        meals.MapGet("/{mealId:guid}", GetMealAsync);
        meals.MapPost("/", CreateMealAsync);
        meals.MapPut("/{mealId:guid}", ReviseMealAsync);
        meals.MapPost("/{mealId:guid}/recipes", AddRecipeAsync);
        meals.MapDelete("/{mealId:guid}/recipes/{recipeSnapshotId:guid}", RemoveRecipeAsync);
        meals.MapPost("/{mealId:guid}/recipes/{recipeSnapshotId:guid}/refresh", RefreshRecipeAsync);
        meals.MapGet("/{mealId:guid}/shopping-draft", PrepareShoppingAsync);
        return endpoints;
    }

    private static async Task<IResult> SearchIngredientsAsync(
        Guid organizationId, string? query, int? limit, HttpContext context,
        IOrganizationCateringLibrary library, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await library.SearchIngredientsAsync(
                new IngredientSearch(actorId, organizationId, query ?? string.Empty, limit ?? 20), cancellationToken))
            : Results.Unauthorized());

    private static async Task<IResult> CreateIngredientAsync(
        Guid organizationId, IngredientBody body, HttpContext context, IAntiforgery antiforgery,
        IOrganizationCateringLibrary library, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await library.CreateIngredientAsync(
                new CreateIngredient(actorId, organizationId, body.Name), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/catering/ingredients/{result.Id:D}", result);
        });
    }

    private static async Task<IResult> RenameIngredientAsync(
        Guid organizationId, Guid ingredientId, IngredientBody body, HttpContext context,
        IAntiforgery antiforgery, IOrganizationCateringLibrary library, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await library.RenameIngredientAsync(
                new RenameIngredient(actorId, organizationId, ingredientId, body.Name, version), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> PreviewMergeAsync(
        Guid organizationId, IngredientMergeBody body, HttpContext context, IAntiforgery antiforgery,
        IOrganizationCateringLibrary library, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () => Results.Ok(await library.PreviewIngredientMergeAsync(
            new IngredientMergeRequest(actorId, organizationId, body.SourceIngredientId, body.TargetIngredientId),
            cancellationToken)));
    }

    private static async Task<IResult> MergeIngredientsAsync(
        Guid organizationId, IngredientMergeBody body, HttpContext context, IAntiforgery antiforgery,
        IOrganizationCateringLibrary library, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () => Results.Ok(await library.MergeIngredientsAsync(
            new MergeIngredients(actorId, organizationId, body.SourceIngredientId, body.TargetIngredientId,
                body.ExpectedSourceVersion, body.ExpectedTargetVersion), cancellationToken)));
    }

    private static async Task<IResult> ListRecipesAsync(
        Guid organizationId, HttpContext context, IOrganizationCateringLibrary library,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await library.ListRecipesAsync(new OrganizationCateringQuery(actorId, organizationId), cancellationToken))
            : Results.Unauthorized());

    private static async Task<IResult> GetRecipeAsync(
        Guid organizationId, Guid recipeId, HttpContext context, IOrganizationCateringLibrary library,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        var result = await library.GetRecipeAsync(new RecipeRequest(actorId, organizationId, recipeId), cancellationToken);
        if (result is null) return Results.NotFound();
        PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
        return Results.Ok(result);
    });

    private static async Task<IResult> CreateRecipeAsync(
        Guid organizationId, RecipeContent body, HttpContext context, IAntiforgery antiforgery,
        IOrganizationCateringLibrary library, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await library.CreateRecipeAsync(new CreateRecipe(actorId, organizationId, body), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/catering/recipes/{result.Id:D}", result);
        });
    }

    private static async Task<IResult> ReviseRecipeAsync(
        Guid organizationId, Guid recipeId, RecipeContent body, HttpContext context, IAntiforgery antiforgery,
        IOrganizationCateringLibrary library, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await library.ReviseRecipeAsync(new ReviseRecipe(actorId, organizationId, recipeId, version, body), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ListMealsAsync(Guid organizationId, Guid campId, HttpContext context,
        ICampMealPlanning planning, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await planning.ListMealsAsync(new CampCateringQuery(actorId, organizationId, campId), cancellationToken))
            : Results.Unauthorized());

    private static async Task<IResult> GetMealAsync(Guid organizationId, Guid campId, Guid mealId,
        HttpContext context, ICampMealPlanning planning, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        var result = await planning.GetMealAsync(new MealRequest(actorId, organizationId, campId, mealId), cancellationToken);
        if (result is null) return Results.NotFound();
        PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
        return Results.Ok(result);
    });

    private static async Task<IResult> CreateMealAsync(Guid organizationId, Guid campId, MealBody body,
        HttpContext context, IAntiforgery antiforgery, ICampMealPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.CreateMealAsync(new CreateMeal(actorId, organizationId, campId, body.Name,
                body.PortionOverride, body.ScheduleEntryId, body.RecipeIds), cancellationToken);
            await UpsertMealActivityAsync(activity, actorId, result, ActivityKind.Created, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/catering/meals/{result.Id:D}", result);
        });
    }

    private static async Task<IResult> ReviseMealAsync(Guid organizationId, Guid campId, Guid mealId, MealBody body,
        HttpContext context, IAntiforgery antiforgery, ICampMealPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.ReviseMealAsync(new ReviseMeal(actorId, organizationId, campId, mealId,
                body.Name, body.PortionOverride, body.ScheduleEntryId, version), cancellationToken);
            await UpsertMealActivityAsync(activity, actorId, result, ActivityKind.Updated, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> AddRecipeAsync(Guid organizationId, Guid campId, Guid mealId,
        RecipeSelectionBody body, HttpContext context, IAntiforgery antiforgery, ICampMealPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken) => await ChangeMealAsync(
        context, antiforgery, activity, async (actorId, version) =>
        await planning.AddRecipeSnapshotAsync(new AddRecipeSnapshot(actorId, organizationId, campId, mealId,
            body.RecipeId, version), cancellationToken));

    private static async Task<IResult> RemoveRecipeAsync(Guid organizationId, Guid campId, Guid mealId,
        Guid recipeSnapshotId, HttpContext context, IAntiforgery antiforgery, ICampMealPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken) => await ChangeMealAsync(
        context, antiforgery, activity, async (actorId, version) =>
        await planning.RemoveRecipeSnapshotAsync(new RemoveRecipeSnapshot(actorId, organizationId, campId, mealId,
            recipeSnapshotId, version), cancellationToken));

    private static async Task<IResult> RefreshRecipeAsync(Guid organizationId, Guid campId, Guid mealId,
        Guid recipeSnapshotId, HttpContext context, IAntiforgery antiforgery, ICampMealPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken) => await ChangeMealAsync(
        context, antiforgery, activity, async (actorId, version) =>
        await planning.RefreshRecipeSnapshotAsync(new RefreshRecipeSnapshot(actorId, organizationId, campId, mealId,
            recipeSnapshotId, version), cancellationToken));

    private static async Task<IResult> ChangeMealAsync(HttpContext context, IAntiforgery antiforgery,
        PlanningActivityWriter activity, Func<Guid, long, Task<Meal>> action)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await action(actorId, version);
            await UpsertMealActivityAsync(activity, actorId, result, ActivityKind.Updated,
                context.RequestAborted);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static Task UpsertMealActivityAsync(PlanningActivityWriter activity, Guid actorId, Meal meal,
        ActivityKind kind, CancellationToken cancellationToken) => activity.UpsertAsync(actorId,
        meal.OrganizationId, meal.CampId, kind, "Meal", meal.Id, meal.Name,
        string.Join(' ', meal.Name,
            string.Join(' ', meal.RecipeSnapshots.Select(recipe => recipe.Name)),
            string.Join(' ', meal.RecipeSnapshots.SelectMany(recipe => recipe.Ingredients)
                .Select(ingredient => ingredient.IngredientName))),
        new Dictionary<string, string>
        {
            ["portions"] = meal.EffectivePortions.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["linked"] = (meal.ScheduleEntryId is not null).ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        },
        meal.Version,
        cancellationToken);

    private static async Task<IResult> PrepareShoppingAsync(Guid organizationId, Guid campId, Guid mealId,
        HttpContext context, IMealShoppingSource source, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await source.PrepareShoppingTransferAsync(new MealRequest(actorId, organizationId, campId, mealId), cancellationToken))
            : Results.Unauthorized());

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (CateringRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message, "Essensplanung nicht möglich");
        }
        catch (ActivityRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message,
                "Aktivität konnte nicht gespeichert werden");
        }
    }

    private sealed record IngredientBody(string Name);
    private sealed record IngredientMergeBody(Guid SourceIngredientId, Guid TargetIngredientId,
        long ExpectedSourceVersion, long ExpectedTargetVersion);
    private sealed record MealBody(string Name, int? PortionOverride, Guid? ScheduleEntryId, IReadOnlyList<Guid> RecipeIds);
    private sealed record RecipeSelectionBody(Guid RecipeId);
}

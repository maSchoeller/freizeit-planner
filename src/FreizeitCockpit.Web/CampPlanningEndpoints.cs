using System.Globalization;
using System.Security.Claims;
using Activity.Contracts;
using Camps.Contracts;
using Catering.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Spiritual.Contracts;

internal static class CampPlanningEndpoints
{
    public static IEndpointRouteBuilder MapCampPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var camps = endpoints.MapGroup("/api/v1/organizations/{organizationId:guid}/camps")
            .RequireAuthorization();
        camps.MapGet("/", ListCampsAsync).Produces<IReadOnlyList<CampSummary>>();
        camps.MapPost("/", CreateCampAsync).Produces<CampView>(StatusCodes.Status201Created);
        camps.MapGet("/by-slug/{campSlug}", GetCampAsync).Produces<CampView>();
        camps.MapPut("/{campId:guid}", UpdateCampAsync)
            .Produces<CampView>()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        camps.MapPatch("/{campId:guid}/status", ChangeCampStatusAsync)
            .Produces<CampView>()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        camps.MapGet("/{campId:guid}/schedule", ListScheduleAsync)
            .Produces<IReadOnlyList<ScheduleEntryView>>();
        camps.MapPost("/{campId:guid}/schedule", CreateScheduleEntryAsync)
            .Produces<ScheduleEntryView>(StatusCodes.Status201Created);
        camps.MapGet("/{campId:guid}/schedule/{scheduleEntryId:guid}", GetScheduleEntryAsync)
            .Produces<ScheduleEntryView>();
        camps.MapPut("/{campId:guid}/schedule/{scheduleEntryId:guid}", UpdateScheduleEntryAsync)
            .Produces<ScheduleEntryView>()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        camps.MapDelete("/{campId:guid}/schedule/{scheduleEntryId:guid}", DeleteScheduleEntryAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        camps.MapPost("/{campId:guid}/schedule/{scheduleEntryId:guid}/restore", RestoreScheduleEntryAsync)
            .Produces<ScheduleEntryView>()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        return endpoints;
    }

    private static async Task<IResult> ListCampsAsync(
        Guid organizationId,
        ClaimsPrincipal principal,
        ICampManagement management,
        CancellationToken cancellationToken) =>
        TryActor(principal, out var actorId)
            ? await ExecuteAsync(async () => Results.Ok(
                await management.ListAsync(
                    new CampListQuery(actorId, organizationId),
                    cancellationToken)))
            : Results.Unauthorized();

    private static async Task<IResult> GetCampAsync(
        Guid organizationId,
        string campSlug,
        HttpContext context,
        ICampManagement management,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await management.GetBySlugAsync(
                new CampBySlugQuery(actorId, organizationId, campSlug),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> CreateCampAsync(
        Guid organizationId,
        CreateCampBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        ICampManagement management,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await management.CreateAsync(
                new CreateCamp(
                    actorId,
                    organizationId,
                    body.Name,
                    body.Slug,
                    body.Description,
                    body.StartsOn,
                    body.EndsOn,
                    body.TimeZoneId,
                    body.DefaultPortions),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Created(
                $"/api/v1/organizations/{organizationId:D}/camps/by-slug/{result.Slug}",
                result);
        });
    }

    private static async Task<IResult> UpdateCampAsync(
        Guid organizationId,
        Guid campId,
        UpdateCampBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        ICampManagement management,
        PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await management.UpdateAsync(
                new UpdateCamp(
                    actorId,
                    organizationId,
                    campId,
                    body.Name,
                    body.Slug,
                    body.Description,
                    body.StartsOn,
                    body.EndsOn,
                    body.TimeZoneId,
                    body.DefaultPortions,
                    version),
                cancellationToken);
            await activity.UpsertAsync(actorId, organizationId, campId, ActivityKind.Updated,
                "Camp", result.Id, result.Name,
                string.Join(' ', result.Name, result.Description, result.Slug),
                new Dictionary<string, string> { ["status"] = result.Status.ToString() },
                result.Version,
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ChangeCampStatusAsync(
        Guid organizationId,
        Guid campId,
        ChangeCampStatusBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        ICampManagement management,
        PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await management.ChangeStatusAsync(
                new ChangeCampStatus(actorId, organizationId, campId, body.Status, version),
                cancellationToken);
            await activity.UpsertAsync(actorId, organizationId, campId, ActivityKind.Updated,
                "Camp", result.Id, result.Name,
                string.Join(' ', result.Name, result.Description, result.Slug),
                new Dictionary<string, string> { ["status"] = result.Status.ToString() },
                result.Version,
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ListScheduleAsync(
        Guid organizationId,
        Guid campId,
        DateOnly fromDate,
        DateOnly toDateExclusive,
        ClaimsPrincipal principal,
        ISchedulePlanning planning,
        CancellationToken cancellationToken) =>
        TryActor(principal, out var actorId)
            ? await ExecuteAsync(async () => Results.Ok(
                await planning.ListAsync(
                    new ScheduleRangeQuery(
                        actorId,
                        organizationId,
                        campId,
                        fromDate,
                        toDateExclusive),
                    cancellationToken)))
            : Results.Unauthorized();

    private static async Task<IResult> GetScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        HttpContext context,
        ISchedulePlanning planning,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.GetAsync(
                new ScheduleEntryQuery(actorId, organizationId, campId, scheduleEntryId),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> CreateScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        ScheduleEntryBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        ISchedulePlanning planning,
        PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.CreateAsync(
                new CreateScheduleEntry(
                    actorId,
                    organizationId,
                    campId,
                    body.Timing,
                    body.Title,
                    body.Description,
                    body.Location,
                    body.Category,
                    body.Status,
                    body.ResponsibleUserIds,
                    body.Audience),
                cancellationToken);
            await activity.UpsertAsync(actorId, organizationId, campId, ActivityKind.Created,
                "ScheduleEntry", result.Id, result.Title,
                string.Join(' ', result.Title, result.Description, result.Location, result.Category, result.Audience),
                new Dictionary<string, string>
                {
                    ["category"] = result.Category,
                    ["status"] = result.Status.ToString()
                },
                result.Version,
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Created(
                $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/schedule/{result.Id:D}",
                result);
        });
    }

    private static async Task<IResult> UpdateScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        ScheduleEntryBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        ISchedulePlanning planning,
        PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.UpdateAsync(
                new UpdateScheduleEntry(
                    actorId,
                    organizationId,
                    campId,
                    scheduleEntryId,
                    body.Timing,
                    body.Title,
                    body.Description,
                    body.Location,
                    body.Category,
                    body.Status,
                    body.ResponsibleUserIds,
                    body.Audience,
                    version),
                cancellationToken);
            await activity.UpsertAsync(actorId, organizationId, campId, ActivityKind.Updated,
                "ScheduleEntry", result.Id, result.Title,
                string.Join(' ', result.Title, result.Description, result.Location, result.Category, result.Audience),
                new Dictionary<string, string>
                {
                    ["category"] = result.Category,
                    ["status"] = result.Status.ToString()
                },
                result.Version,
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> DeleteScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        LinkedScheduleDeleteBehavior? linkedBehavior,
        HttpContext context,
        IAntiforgery antiforgery,
        ISchedulePlanning planning,
        ICampMealPlanning meals,
        IDevotionPlanning devotions,
        PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var current = await planning.GetAsync(
                new ScheduleEntryQuery(actorId, organizationId, campId, scheduleEntryId),
                cancellationToken);
            var linkedMeals = (await meals.ListMealsAsync(
                    new CampCateringQuery(actorId, organizationId, campId), cancellationToken))
                .Where(item => item.ScheduleEntryId == scheduleEntryId)
                .ToArray();
            var linkedDevotions = (await devotions.ListAsync(
                    new DevotionScope(actorId, organizationId, campId), cancellationToken))
                .Where(item => item.ScheduleEntryId == scheduleEntryId)
                .ToArray();
            if (linkedMeals.Length + linkedDevotions.Length > 0 && linkedBehavior is null)
            {
                return PlanningEndpointSupport.Problem(
                    "linked_delete_choice_required",
                    "Wähle ausdrücklich, ob verknüpfte Mahlzeiten und Andachten entkoppelt oder ebenfalls in den Papierkorb verschoben werden.",
                    "Löschentscheidung erforderlich");
            }
            foreach (var meal in linkedMeals)
            {
                if (linkedBehavior == LinkedScheduleDeleteBehavior.MoveLinkedToTrash)
                {
                    await meals.MoveMealToTrashAsync(
                        new DeleteMeal(actorId, organizationId, campId, meal.Id, meal.Version), cancellationToken);
                    await activity.RemoveAsync(actorId, organizationId, campId, "Meal", meal.Id, meal.Name,
                        meal.Version + 1, cancellationToken);
                }
                else
                {
                    var details = await meals.GetMealAsync(
                        new MealRequest(actorId, organizationId, campId, meal.Id), cancellationToken)
                        ?? throw new CateringRuleException("meal_not_found", "Die Mahlzeit wurde nicht gefunden.");
                    var unlinked = await meals.ReviseMealAsync(
                        new ReviseMeal(actorId, organizationId, campId, meal.Id, details.Name,
                            details.PortionOverride, null, details.Version), cancellationToken);
                    await RecordMealUpdateAsync(activity, actorId, unlinked, cancellationToken);
                }
            }
            foreach (var devotion in linkedDevotions)
            {
                if (linkedBehavior == LinkedScheduleDeleteBehavior.MoveLinkedToTrash)
                {
                    await devotions.MoveToTrashAsync(
                        new ChangeDevotionLifecycle(actorId, organizationId, campId, devotion.Id,
                            devotion.Version), cancellationToken);
                    await activity.RemoveAsync(actorId, organizationId, campId, "Devotion", devotion.Id,
                        devotion.Topic, devotion.Version + 1, cancellationToken);
                }
                else
                {
                    var details = await devotions.GetAsync(
                        new DevotionKey(actorId, organizationId, campId, devotion.Id), cancellationToken)
                        ?? throw new SpiritualRuleException("devotion_not_found", "Die Andacht wurde nicht gefunden.");
                    var unlinked = await devotions.UpdateAsync(
                        new UpdateDevotion(actorId, organizationId, campId, devotion.Id, details.Topic,
                            details.BibleReference, details.Translation, details.CoreMessage,
                            details.MarkdownContent, details.ResponsibleUserIds, details.MaterialNotes, null,
                            details.Version), cancellationToken);
                    await RecordDevotionUpdateAsync(activity, actorId, unlinked, cancellationToken);
                }
            }
            var deleted = await planning.DeleteAsync(
                new DeleteScheduleEntry(actorId, organizationId, campId, scheduleEntryId, version),
                cancellationToken);
            await activity.RemoveAsync(actorId, organizationId, campId, "ScheduleEntry", scheduleEntryId,
                current.Title, deleted.Version, cancellationToken);
            return Results.NoContent();
        });
    }

    private static Task RecordMealUpdateAsync(
        PlanningActivityWriter activity,
        Guid actorId,
        Meal meal,
        CancellationToken cancellationToken) => activity.UpsertAsync(
        actorId,
        meal.OrganizationId,
        meal.CampId,
        ActivityKind.Updated,
        "Meal",
        meal.Id,
        meal.Name,
        string.Join(' ', meal.Name,
            string.Join(' ', meal.RecipeSnapshots.Select(recipe => recipe.Name)),
            string.Join(' ', meal.RecipeSnapshots.SelectMany(recipe => recipe.Ingredients)
                .Select(ingredient => ingredient.IngredientName))),
        new Dictionary<string, string>
        {
            ["portions"] = meal.EffectivePortions.ToString(CultureInfo.InvariantCulture),
            ["linked"] = bool.FalseString
        },
        meal.Version,
        cancellationToken);

    private static Task RecordDevotionUpdateAsync(
        PlanningActivityWriter activity,
        Guid actorId,
        DevotionDetails devotion,
        CancellationToken cancellationToken) => activity.UpsertAsync(
        actorId,
        devotion.OrganizationId,
        devotion.CampId,
        ActivityKind.Updated,
        "Devotion",
        devotion.Id,
        devotion.Topic,
        string.Join(' ', devotion.Topic, devotion.BibleReference, devotion.CoreMessage,
            devotion.MarkdownContent, devotion.MaterialNotes),
        new Dictionary<string, string>
        {
            ["translation"] = devotion.Translation.ToString(),
            ["hasSnapshot"] = (devotion.BibleSnapshot is not null).ToString(CultureInfo.InvariantCulture)
        },
        devotion.Version,
        cancellationToken);

    private static async Task<IResult> RestoreScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        HttpContext context,
        IAntiforgery antiforgery,
        ISchedulePlanning planning,
        PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var restored = await planning.RestoreAsync(
                new RestoreScheduleEntry(actorId, organizationId, campId, scheduleEntryId, version),
                cancellationToken);
            await activity.UpsertAsync(
                actorId,
                organizationId,
                campId,
                ActivityKind.Restored,
                "ScheduleEntry",
                restored.Id,
                restored.Title,
                string.Join(' ', restored.Title, restored.Description, restored.Location, restored.Category,
                    restored.Audience),
                new Dictionary<string, string>
                {
                    ["category"] = restored.Category,
                    ["status"] = restored.Status.ToString()
                },
                restored.Version,
                cancellationToken);
            WriteEtag(context.Response, restored.Version);
            return Results.Ok(restored);
        });
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (CampsRuleException exception)
        {
            var status = exception.ErrorCode switch
            {
                "version_conflict" => StatusCodes.Status412PreconditionFailed,
                "camp_not_found" or "schedule_entry_not_found" => StatusCodes.Status404NotFound,
                "camp_access_denied" or "schedule_access_denied" => StatusCodes.Status403Forbidden,
                "camp_archived" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.Problem(
                statusCode: status,
                title: "Planung nicht möglich",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = exception.ErrorCode });
        }
        catch (ActivityRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message,
                "Aktivität konnte nicht gespeichert werden");
        }
        catch (CateringRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message,
                "Verknüpfte Mahlzeit konnte nicht verarbeitet werden");
        }
        catch (SpiritualRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message,
                "Verknüpfte Andacht konnte nicht verarbeitet werden");
        }
    }

    private static ValueTask<IResult?> ValidateMutationAsync(
        HttpContext context,
        IAntiforgery antiforgery) =>
        IdentityEndpoints.ValidateAntiforgeryAsync(context, antiforgery);

    private static bool TryActor(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private static bool TryReadVersion(HttpRequest request, out long version)
    {
        version = default;
        var value = request.Headers.IfMatch.ToString().Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        return long.TryParse(value.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version)
            && version > 0;
    }

    private static IResult PreconditionRequired() => Results.Problem(
        statusCode: StatusCodes.Status428PreconditionRequired,
        title: "Versionsangabe erforderlich",
        detail: "Sende die zuletzt gelesene Version im If-Match-Header.",
        extensions: new Dictionary<string, object?> { ["errorCode"] = "if_match_required" });

    private static void WriteEtag(HttpResponse response, long version) =>
        response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    private sealed record CreateCampBody(
        string Name,
        string Slug,
        string? Description,
        DateOnly StartsOn,
        DateOnly EndsOn,
        string? TimeZoneId,
        int DefaultPortions);

    private sealed record UpdateCampBody(
        string Name,
        string Slug,
        string? Description,
        DateOnly StartsOn,
        DateOnly EndsOn,
        string TimeZoneId,
        int DefaultPortions);

    private sealed record ChangeCampStatusBody(CampStatus Status);

    private sealed record ScheduleEntryBody(
        ScheduleTimingInput Timing,
        string Title,
        string? Description,
        string? Location,
        string Category,
        ScheduleEntryStatus Status,
        IReadOnlyList<Guid> ResponsibleUserIds,
        string? Audience);
}

internal enum LinkedScheduleDeleteBehavior
{
    Unlink,
    MoveLinkedToTrash
}

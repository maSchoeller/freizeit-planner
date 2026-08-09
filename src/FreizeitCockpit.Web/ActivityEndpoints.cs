using Activity.Contracts;
using Camps.Contracts;
using Catering.Contracts;
using Logistics.Contracts;

internal static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/camps/{campId:guid}")
            .RequireAuthorization();
        group.MapGet("/activity", ListActivityAsync);
        group.MapGet("/search", SearchAsync);
        group.MapGet("/exports/schedule.csv", ExportScheduleAsync);
        group.MapGet("/exports/meals.csv", ExportMealsAsync);
        group.MapGet("/exports/material.csv", ExportMaterialAsync);
        group.MapGet("/exports/shopping.csv", ExportShoppingAsync);
        return endpoints;
    }

    private static async Task<IResult> ListActivityAsync(Guid organizationId, Guid campId, string? kinds,
        string? objectTypes, Guid? actorFilter, DateTimeOffset? before, int? limit, HttpContext context,
        IActivityJournal journal, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await journal.ListAsync(new ActivityQuery(actorId, organizationId, campId,
                ParseKinds(kinds), Split(objectTypes), actorFilter, before, limit ?? 50), cancellationToken))
            : Results.Unauthorized());

    private static async Task<IResult> SearchAsync(Guid organizationId, Guid campId, string query,
        string? objectTypes, string? metadata, int? limit, HttpContext context, ICampSearchIndex search,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await search.SearchAsync(new CampSearchQuery(actorId, organizationId, campId, query,
                Split(objectTypes), ParseMetadata(metadata), limit ?? 50), cancellationToken)) : Results.Unauthorized());

    private static async Task<IResult> ExportScheduleAsync(Guid organizationId, Guid campId, DateOnly fromDate,
        DateOnly toDateExclusive, HttpContext context, ISchedulePlanning schedule, ICampExportFormatter formatter,
        CancellationToken cancellationToken)
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var rows = (await schedule.ListAsync(new ScheduleRangeQuery(actorId, organizationId, campId,
                fromDate, toDateExclusive), cancellationToken)).Select(item => (IReadOnlyList<string?>)
                [item.Title, item.Category, item.Timing.StartsAtUtc?.ToString("O"),
                    item.Timing.EndsAtUtc?.ToString("O"), item.Location]).ToArray();
            return await CsvAsync(formatter, actorId, organizationId, campId,
                ["Titel", "Kategorie", "Beginn", "Ende", "Ort"], rows, "tagesplan.csv", cancellationToken);
        });
    }

    private static async Task<IResult> ExportMealsAsync(Guid organizationId, Guid campId, HttpContext context,
        ICampMealPlanning meals, ICampExportFormatter formatter, CancellationToken cancellationToken)
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var rows = (await meals.ListMealsAsync(new CampCateringQuery(actorId, organizationId, campId),
                cancellationToken)).Select(item => (IReadOnlyList<string?>)[item.Name,
                    item.EffectivePortions.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    item.RecipeCount.ToString(System.Globalization.CultureInfo.InvariantCulture)]).ToArray();
            return await CsvAsync(formatter, actorId, organizationId, campId,
                ["Mahlzeit", "Portionen", "Rezepte"], rows, "mahlzeiten.csv", cancellationToken);
        });
    }

    private static async Task<IResult> ExportMaterialAsync(Guid organizationId, Guid campId, HttpContext context,
        IMaterialPlanning materials, ICampExportFormatter formatter, CancellationToken cancellationToken)
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var rows = (await materials.ListAsync(new MaterialQuery(actorId, organizationId, campId),
                cancellationToken)).Select(item => (IReadOnlyList<string?>)[item.Name,
                    item.Quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    item.Quantity.CustomUnitName ?? item.Quantity.Unit.ToString(), item.Status.ToString()]).ToArray();
            return await CsvAsync(formatter, actorId, organizationId, campId,
                ["Material", "Menge", "Einheit", "Status"], rows, "material.csv", cancellationToken);
        });
    }

    private static async Task<IResult> ExportShoppingAsync(Guid organizationId, Guid campId, Guid? listId,
        HttpContext context, IShoppingPlanning shopping, ICampExportFormatter formatter,
        CancellationToken cancellationToken)
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var listIds = listId is { } requestedListId
                ? [requestedListId]
                : (await shopping.ListAsync(new ShoppingListsQuery(actorId, organizationId, campId),
                    cancellationToken)).Select(item => item.Id).ToArray();
            var rows = new List<IReadOnlyList<string?>>();
            foreach (var currentListId in listIds)
            {
                var list = await shopping.GetAsync(
                    new ShoppingListRequest(actorId, organizationId, campId, currentListId), cancellationToken);
                if (list is null)
                {
                    if (listId is not null) return Results.NotFound();
                    continue;
                }
                rows.AddRange(list.Items.Select(item => (IReadOnlyList<string?>)[list.Name, item.Name,
                    item.Quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    item.Quantity.CustomUnitName ?? item.Quantity.Unit.ToString(), item.Store,
                    item.IsChecked ? "Ja" : "Nein", item.Source.Label]));
            }
            return await CsvAsync(formatter, actorId, organizationId, campId,
                ["Liste", "Position", "Menge", "Einheit", "Geschäft", "Erledigt", "Quelle"], rows,
                "einkauf.csv", cancellationToken);
        });
    }

    private static async Task<IResult> CsvAsync(ICampExportFormatter formatter, Guid actorId,
        Guid organizationId, Guid campId, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows,
        string fileName, CancellationToken cancellationToken)
    {
        var document = await formatter.FormatAsync(new CampCsvRequest(actorId, organizationId, campId,
            headers, rows), cancellationToken);
        return Results.File(document.Content.ToArray(), document.MediaType, fileName);
    }

    private static string[]? Split(string? values) => string.IsNullOrWhiteSpace(values)
        ? null : values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static ActivityKind[]? ParseKinds(string? values)
    {
        var parts = Split(values);
        if (parts is null) return null;
        var result = new ActivityKind[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!Enum.TryParse(parts[index], true, out result[index]))
                throw new ActivityRuleException("activity_kind_invalid", "Der Aktivitätsfilter ist ungültig.");
        }
        return result;
    }

    private static Dictionary<string, string>? ParseMetadata(string? value)
    {
        var parts = Split(value);
        if (parts is null) return null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            var separator = part.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0 || separator == part.Length - 1 ||
                !result.TryAdd(part[..separator], part[(separator + 1)..]))
                throw new ActivityRuleException("search_metadata_invalid", "Der Metadatenfilter ist ungültig.");
        }
        return result;
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (ActivityRuleException exception)
        { return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message, "Aktivität nicht verfügbar"); }
    }
}

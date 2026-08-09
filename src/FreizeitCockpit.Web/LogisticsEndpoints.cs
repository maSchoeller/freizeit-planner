using Activity.Contracts;
using Logistics.Contracts;
using Catering.Contracts;
using Microsoft.AspNetCore.Antiforgery;

internal static class LogisticsEndpoints
{
    public static IEndpointRouteBuilder MapLogisticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/camps/{campId:guid}/logistics")
            .RequireAuthorization();
        group.MapGet("/material", ListMaterialAsync);
        group.MapGet("/material/{materialId:guid}", GetMaterialAsync);
        group.MapPost("/material", CreateMaterialAsync);
        group.MapPut("/material/{materialId:guid}", UpdateMaterialAsync);
        group.MapDelete("/material/{materialId:guid}", DeleteMaterialAsync);
        group.MapPost("/material/{materialId:guid}/restore", RestoreMaterialAsync);
        group.MapGet("/shopping-lists", ListShoppingListsAsync);
        group.MapGet("/shopping-lists/{listId:guid}", GetShoppingListAsync);
        group.MapPost("/shopping-lists", CreateShoppingListAsync);
        group.MapPut("/shopping-lists/{listId:guid}", RenameShoppingListAsync);
        group.MapDelete("/shopping-lists/{listId:guid}", DeleteShoppingListAsync);
        group.MapPost("/shopping-lists/{listId:guid}/restore", RestoreShoppingListAsync);
        group.MapPost("/shopping-lists/{listId:guid}/items", AddShoppingItemAsync);
        group.MapPut("/shopping-lists/{listId:guid}/items/{itemId:guid}", UpdateShoppingItemAsync);
        group.MapPatch("/shopping-lists/{listId:guid}/items/{itemId:guid}/checked", CheckShoppingItemAsync);
        group.MapDelete("/shopping-lists/{listId:guid}/items/{itemId:guid}", DeleteShoppingItemAsync);
        group.MapGet("/shopping-lists/{listId:guid}/items/{itemId:guid}/audit", ListAuditAsync);
        group.MapPost("/shopping-lists/{listId:guid}/transfer/material/{materialId:guid}", TransferMaterialAsync);
        group.MapPost("/shopping-lists/{listId:guid}/transfer/meal/{mealId:guid}", TransferMealAsync);
        return endpoints;
    }

    private static async Task<IResult> ListMaterialAsync(Guid organizationId, Guid campId, Guid? scheduleEntryId,
        ProcurementStatus? status, HttpContext context, IMaterialPlanning planning, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await planning.ListAsync(new MaterialQuery(actorId, organizationId, campId,
                scheduleEntryId, status), cancellationToken)) : Results.Unauthorized());

    private static async Task<IResult> GetMaterialAsync(Guid organizationId, Guid campId, Guid materialId,
        HttpContext context, IMaterialPlanning planning, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        var result = await planning.GetAsync(new MaterialRequest(actorId, organizationId, campId, materialId), cancellationToken);
        if (result is null) return Results.NotFound();
        PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
        return Results.Ok(result);
    });

    private static async Task<IResult> CreateMaterialAsync(Guid organizationId, Guid campId, MaterialBody body,
        HttpContext context, IAntiforgery antiforgery, IMaterialPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.CreateAsync(body.Create(actorId, organizationId, campId), cancellationToken);
            await UpsertMaterialActivityAsync(activity, actorId, result, ActivityKind.Created, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/logistics/material/{result.Id:D}", result);
        });
    }

    private static async Task<IResult> UpdateMaterialAsync(Guid organizationId, Guid campId, Guid materialId,
        MaterialBody body, HttpContext context, IAntiforgery antiforgery, IMaterialPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.UpdateAsync(body.Update(actorId, organizationId, campId, materialId, version), cancellationToken);
            await UpsertMaterialActivityAsync(activity, actorId, result, ActivityKind.Updated, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> DeleteMaterialAsync(Guid organizationId, Guid campId, Guid materialId,
        HttpContext context, IAntiforgery antiforgery, IMaterialPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var current = await planning.GetAsync(
                new MaterialRequest(actorId, organizationId, campId, materialId), cancellationToken);
            if (current is null) return Results.NotFound();
            await planning.DeleteAsync(new DeleteMaterialRequirement(actorId,
                organizationId, campId, materialId, version), cancellationToken);
            await activity.RemoveAsync(actorId, organizationId, campId, "MaterialRequirement", materialId,
                current.Name, version + 1, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> RestoreMaterialAsync(
        Guid organizationId,
        Guid campId,
        Guid materialId,
        HttpContext context,
        IAntiforgery antiforgery,
        IMaterialPlanning planning,
        PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version))
            return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var restored = await planning.RestoreAsync(
                new RestoreMaterialRequirement(actorId, organizationId, campId, materialId, version),
                cancellationToken);
            await UpsertMaterialActivityAsync(activity, actorId, restored, ActivityKind.Restored, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, restored.Version);
            return Results.Ok(restored);
        });
    }

    private static async Task<IResult> ListShoppingListsAsync(Guid organizationId, Guid campId, HttpContext context,
        IShoppingPlanning planning, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await planning.ListAsync(new ShoppingListsQuery(actorId, organizationId, campId), cancellationToken))
            : Results.Unauthorized());

    private static async Task<IResult> GetShoppingListAsync(Guid organizationId, Guid campId, Guid listId,
        HttpContext context, IShoppingPlanning planning, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        var result = await planning.GetAsync(new ShoppingListRequest(actorId, organizationId, campId, listId), cancellationToken);
        if (result is null) return Results.NotFound();
        PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
        context.Response.Headers["X-Change-Sequence"] = result.ChangeSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Ok(result);
    });

    private static async Task<IResult> CreateShoppingListAsync(Guid organizationId, Guid campId, NameBody body,
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.CreateListAsync(new CreateShoppingList(actorId, organizationId, campId, body.Name), cancellationToken);
            await UpsertShoppingActivityAsync(activity, actorId, result, ActivityKind.Created, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/logistics/shopping-lists/{result.Id:D}", result);
        });
    }

    private static Task<IResult> RenameShoppingListAsync(Guid organizationId, Guid campId, Guid listId, NameBody body,
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken) =>
        ChangeListAsync(context, antiforgery, activity, async (actorId, version) => await planning.RenameListAsync(
            new RenameShoppingList(actorId, organizationId, campId, listId, body.Name, version), cancellationToken));

    private static async Task<IResult> DeleteShoppingListAsync(Guid organizationId, Guid campId, Guid listId,
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var current = await planning.GetAsync(
                new ShoppingListRequest(actorId, organizationId, campId, listId), cancellationToken);
            if (current is null) return Results.NotFound();
            await planning.DeleteListAsync(new DeleteShoppingList(actorId,
                organizationId, campId, listId, version), cancellationToken);
            await activity.RemoveAsync(actorId, organizationId, campId, "ShoppingList", listId,
                current.Name, version + 1, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> RestoreShoppingListAsync(
        Guid organizationId,
        Guid campId,
        Guid listId,
        HttpContext context,
        IAntiforgery antiforgery,
        IShoppingPlanning planning,
        PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version))
            return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var restored = await planning.RestoreListAsync(
                new RestoreShoppingList(actorId, organizationId, campId, listId, version),
                cancellationToken);
            await UpsertShoppingActivityAsync(activity, actorId, restored, ActivityKind.Restored, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, restored.Version);
            return Results.Ok(restored);
        });
    }

    private static async Task<IResult> AddShoppingItemAsync(Guid organizationId, Guid campId, Guid listId,
        ShoppingItemContent body, HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.AddSpontaneousItemAsync(
                new AddSpontaneousShoppingItem(actorId, organizationId, campId, listId, body, version),
                cancellationToken);
            await RefreshShoppingActivityAsync(planning, activity, actorId, organizationId, campId, listId,
                cancellationToken);
            return Results.Ok(result);
        });
    }

    private static Task<IResult> UpdateShoppingItemAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId,
        ShoppingItemContent body, HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken) => ChangeItemAsync(
        organizationId, campId, listId, context, antiforgery, planning, activity, async (actorId, version) =>
        await planning.UpdateItemAsync(new UpdateShoppingItem(actorId, organizationId, campId, listId, itemId,
            body, version), cancellationToken));

    private static Task<IResult> CheckShoppingItemAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId,
        CheckedBody body, HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken) => ChangeItemAsync(
        organizationId, campId, listId, context, antiforgery, planning, activity, async (actorId, version) =>
        await planning.SetItemCheckedAsync(new SetShoppingItemChecked(actorId, organizationId, campId, listId,
            itemId, body.IsChecked, version), cancellationToken));

    private static Task<IResult> DeleteShoppingItemAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId,
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken) =>
        ChangeItemAsync(organizationId, campId, listId, context, antiforgery, planning, activity,
            async (actorId, version) => await planning.DeleteItemAsync(
            new DeleteShoppingItem(actorId, organizationId, campId, listId, itemId, version), cancellationToken));

    private static async Task<IResult> ListAuditAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId,
        HttpContext context, IShoppingAudit audit, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await audit.ListCheckEventsAsync(new ShoppingCheckAuditQuery(actorId, organizationId,
                campId, listId, itemId), cancellationToken)) : Results.Unauthorized());

    private static async Task<IResult> TransferMaterialAsync(Guid organizationId, Guid campId, Guid listId,
        Guid materialId, TransferMaterialBody body, HttpContext context, IAntiforgery antiforgery,
        IShoppingTransfer transfer, IShoppingPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await transfer.TransferMaterialAsync(new TransferMaterialRequirement(actorId,
                organizationId, campId, listId, body.ExpectedListVersion, materialId,
                body.ExpectedRequirementVersion, body.Content), cancellationToken);
            await RefreshShoppingActivityAsync(planning, activity, actorId, organizationId, campId, listId,
                cancellationToken);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> TransferMealAsync(Guid organizationId, Guid campId, Guid listId,
        Guid mealId, TransferMealBody body, HttpContext context, IAntiforgery antiforgery,
        IMealShoppingSource mealSource, IShoppingTransfer transfer, IShoppingPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var draft = await mealSource.PrepareShoppingTransferAsync(
                new MealRequest(actorId, organizationId, campId, mealId), cancellationToken);
            var available = draft.Lines.ToDictionary(line => (line.RecipeSnapshotId, line.SnapshotIngredientId));
            var lines = new List<CateringShoppingLine>(body.Lines.Count);
            foreach (var edit in body.Lines)
            {
                if (!available.TryGetValue((edit.RecipeSnapshotId, edit.SnapshotIngredientId), out var source))
                    throw new LogisticsRuleException("invalid_catering_source", "Die ausgewählte Rezeptposition ist nicht mehr verfügbar.");
                lines.Add(new CateringShoppingLine(new CateringSourceReference(draft.MealId,
                    source.RecipeSnapshotId, source.SnapshotIngredientId, source.SourceRecipeId,
                    source.SourceRecipeVersionNumber), source.SourceLabel, edit.Content));
            }
            var result = await transfer.TransferCateringAsync(new TransferCateringShoppingItems(actorId,
                organizationId, campId, listId, body.ExpectedListVersion, lines), cancellationToken);
            await RefreshShoppingActivityAsync(planning, activity, actorId, organizationId, campId, listId,
                cancellationToken);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ChangeListAsync(HttpContext context, IAntiforgery antiforgery,
        PlanningActivityWriter activity, Func<Guid, long, Task<ShoppingList>> action)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await action(actorId, version);
            await UpsertShoppingActivityAsync(activity, actorId, result, ActivityKind.Updated,
                context.RequestAborted);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version); return Results.Ok(result);
        });
    }

    private static async Task<IResult> ChangeItemAsync(Guid organizationId, Guid campId, Guid listId,
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning,
        PlanningActivityWriter activity, Func<Guid, long, Task<ShoppingListChange>> action)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await action(actorId, version);
            await RefreshShoppingActivityAsync(planning, activity, actorId, organizationId, campId, listId,
                context.RequestAborted);
            context.Response.Headers["X-Change-Sequence"] = result.ChangeSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.Ok(result);
        });
    }

    private static Task UpsertMaterialActivityAsync(PlanningActivityWriter activity, Guid actorId,
        MaterialRequirement material, ActivityKind kind, CancellationToken cancellationToken) => activity.UpsertAsync(
        actorId, material.OrganizationId, material.CampId, kind, "MaterialRequirement", material.Id, material.Name,
        string.Join(' ', material.Name, material.Description, material.ProcurementSource, material.Note,
            material.Quantity.CustomUnitName, material.Quantity.Unit.ToString()),
        new Dictionary<string, string>
        {
            ["status"] = material.Status.ToString(),
            ["linked"] = (material.ScheduleEntryId is not null).ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        },
        material.Version,
        cancellationToken);

    private static async Task RefreshShoppingActivityAsync(IShoppingPlanning planning,
        PlanningActivityWriter activity, Guid actorId, Guid organizationId, Guid campId, Guid listId,
        CancellationToken cancellationToken)
    {
        var list = await planning.GetAsync(
            new ShoppingListRequest(actorId, organizationId, campId, listId), cancellationToken)
            ?? throw new LogisticsRuleException("shopping_list_not_found", "Die Einkaufsliste wurde nicht gefunden.");
        await UpsertShoppingActivityAsync(activity, actorId, list, ActivityKind.Updated, cancellationToken);
    }

    private static Task UpsertShoppingActivityAsync(PlanningActivityWriter activity, Guid actorId,
        ShoppingList list, ActivityKind kind, CancellationToken cancellationToken) => activity.UpsertAsync(actorId,
        list.OrganizationId, list.CampId, kind, "ShoppingList", list.Id, list.Name,
        string.Join(' ', list.Name,
            string.Join(' ', list.Items.Select(item => item.Name)),
            string.Join(' ', list.Items.Select(item => item.Store)),
            string.Join(' ', list.Items.Select(item => item.Note)),
            string.Join(' ', list.Items.Select(item => item.Source.Label))),
        new Dictionary<string, string>
        {
            ["open"] = list.Items.Count(item => !item.IsChecked).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["checked"] = list.Items.Count(item => item.IsChecked).ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        },
        list.Version,
        cancellationToken);

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (LogisticsRuleException exception)
        { return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message, "Logistikplanung nicht möglich"); }
        catch (ActivityRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message,
            "Aktivität konnte nicht gespeichert werden");
        }
    }

    private sealed record NameBody(string Name);
    private sealed record CheckedBody(bool IsChecked);
    private sealed record TransferMaterialBody(long ExpectedListVersion, long ExpectedRequirementVersion, ShoppingItemContent Content);
    private sealed record TransferMealBody(long ExpectedListVersion, IReadOnlyList<TransferMealLineBody> Lines);
    private sealed record TransferMealLineBody(Guid RecipeSnapshotId, Guid SnapshotIngredientId, ShoppingItemContent Content);
    private sealed record MaterialBody(string Name, string? Description, LogisticsQuantity Quantity,
        IReadOnlyList<Guid> ResponsibleUserIds, string? ProcurementSource, string? Note,
        ProcurementStatus Status, Guid? ScheduleEntryId)
    {
        public CreateMaterialRequirement Create(Guid actorId, Guid organizationId, Guid campId) => new(actorId,
            organizationId, campId, Name, Description, Quantity, ResponsibleUserIds, ProcurementSource, Note,
            Status, ScheduleEntryId);
        public UpdateMaterialRequirement Update(Guid actorId, Guid organizationId, Guid campId, Guid materialId,
            long version) => new(actorId, organizationId, campId, materialId, Name, Description, Quantity,
            ResponsibleUserIds, ProcurementSource, Note, Status, ScheduleEntryId, version);
    }
}

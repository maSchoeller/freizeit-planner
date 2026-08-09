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
        group.MapGet("/shopping-lists", ListShoppingListsAsync);
        group.MapGet("/shopping-lists/{listId:guid}", GetShoppingListAsync);
        group.MapPost("/shopping-lists", CreateShoppingListAsync);
        group.MapPut("/shopping-lists/{listId:guid}", RenameShoppingListAsync);
        group.MapDelete("/shopping-lists/{listId:guid}", DeleteShoppingListAsync);
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
        HttpContext context, IAntiforgery antiforgery, IMaterialPlanning planning, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.CreateAsync(body.Create(actorId, organizationId, campId), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/logistics/material/{result.Id:D}", result);
        });
    }

    private static async Task<IResult> UpdateMaterialAsync(Guid organizationId, Guid campId, Guid materialId,
        MaterialBody body, HttpContext context, IAntiforgery antiforgery, IMaterialPlanning planning,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.UpdateAsync(body.Update(actorId, organizationId, campId, materialId, version), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> DeleteMaterialAsync(Guid organizationId, Guid campId, Guid materialId,
        HttpContext context, IAntiforgery antiforgery, IMaterialPlanning planning, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            await planning.DeleteAsync(new DeleteMaterialRequirement(actorId,
            organizationId, campId, materialId, version), cancellationToken); return Results.NoContent();
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
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.CreateListAsync(new CreateShoppingList(actorId, organizationId, campId, body.Name), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/logistics/shopping-lists/{result.Id:D}", result);
        });
    }

    private static Task<IResult> RenameShoppingListAsync(Guid organizationId, Guid campId, Guid listId, NameBody body,
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning, CancellationToken cancellationToken) =>
        ChangeListAsync(context, antiforgery, async (actorId, version) => await planning.RenameListAsync(
            new RenameShoppingList(actorId, organizationId, campId, listId, body.Name, version), cancellationToken));

    private static async Task<IResult> DeleteShoppingListAsync(Guid organizationId, Guid campId, Guid listId,
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            await planning.DeleteListAsync(new DeleteShoppingList(actorId,
            organizationId, campId, listId, version), cancellationToken); return Results.NoContent();
        });
    }

    private static async Task<IResult> AddShoppingItemAsync(Guid organizationId, Guid campId, Guid listId,
        ShoppingItemContent body, HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () => Results.Ok(await planning.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(actorId, organizationId, campId, listId, body, version), cancellationToken)));
    }

    private static Task<IResult> UpdateShoppingItemAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId,
        ShoppingItemContent body, HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning,
        CancellationToken cancellationToken) => ChangeItemAsync(context, antiforgery, async (actorId, version) =>
        await planning.UpdateItemAsync(new UpdateShoppingItem(actorId, organizationId, campId, listId, itemId,
            body, version), cancellationToken));

    private static Task<IResult> CheckShoppingItemAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId,
        CheckedBody body, HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning,
        CancellationToken cancellationToken) => ChangeItemAsync(context, antiforgery, async (actorId, version) =>
        await planning.SetItemCheckedAsync(new SetShoppingItemChecked(actorId, organizationId, campId, listId,
            itemId, body.IsChecked, version), cancellationToken));

    private static Task<IResult> DeleteShoppingItemAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId,
        HttpContext context, IAntiforgery antiforgery, IShoppingPlanning planning, CancellationToken cancellationToken) =>
        ChangeItemAsync(context, antiforgery, async (actorId, version) => await planning.DeleteItemAsync(
            new DeleteShoppingItem(actorId, organizationId, campId, listId, itemId, version), cancellationToken));

    private static async Task<IResult> ListAuditAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId,
        HttpContext context, IShoppingAudit audit, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await audit.ListCheckEventsAsync(new ShoppingCheckAuditQuery(actorId, organizationId,
                campId, listId, itemId), cancellationToken)) : Results.Unauthorized());

    private static async Task<IResult> TransferMaterialAsync(Guid organizationId, Guid campId, Guid listId,
        Guid materialId, TransferMaterialBody body, HttpContext context, IAntiforgery antiforgery,
        IShoppingTransfer transfer, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () => Results.Ok(await transfer.TransferMaterialAsync(
            new TransferMaterialRequirement(actorId, organizationId, campId, listId, body.ExpectedListVersion,
                materialId, body.ExpectedRequirementVersion, body.Content), cancellationToken)));
    }

    private static async Task<IResult> TransferMealAsync(Guid organizationId, Guid campId, Guid listId,
        Guid mealId, TransferMealBody body, HttpContext context, IAntiforgery antiforgery,
        IMealShoppingSource mealSource, IShoppingTransfer transfer, CancellationToken cancellationToken)
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
            return Results.Ok(await transfer.TransferCateringAsync(new TransferCateringShoppingItems(actorId,
                organizationId, campId, listId, body.ExpectedListVersion, lines), cancellationToken));
        });
    }

    private static async Task<IResult> ChangeListAsync(HttpContext context, IAntiforgery antiforgery,
        Func<Guid, long, Task<ShoppingList>> action)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await action(actorId, version);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version); return Results.Ok(result);
        });
    }

    private static async Task<IResult> ChangeItemAsync(HttpContext context, IAntiforgery antiforgery,
        Func<Guid, long, Task<ShoppingListChange>> action)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await action(actorId, version);
            context.Response.Headers["X-Change-Sequence"] = result.ChangeSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (LogisticsRuleException exception)
        { return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message, "Logistikplanung nicht möglich"); }
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

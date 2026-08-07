using Microsoft.AspNetCore.Antiforgery;
using Spiritual.Contracts;

internal static class SpiritualEndpoints
{
    public static IEndpointRouteBuilder MapSpiritualEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/camps/{campId:guid}/devotions")
            .RequireAuthorization();
        group.MapGet("/", ListAsync);
        group.MapGet("/translations", ListTranslationsAsync);
        group.MapGet("/{devotionId:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{devotionId:guid}", UpdateAsync);
        group.MapDelete("/{devotionId:guid}", TrashAsync);
        group.MapPost("/{devotionId:guid}/restore", RestoreAsync);
        group.MapPost("/{devotionId:guid}/bible/refresh", RefreshSnapshotAsync);
        group.MapPut("/{devotionId:guid}/bible/manual", SaveManualSnapshotAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(Guid organizationId, Guid campId, HttpContext context,
        IDevotionPlanning planning, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await planning.ListAsync(new DevotionScope(actorId, organizationId, campId), cancellationToken))
            : Results.Unauthorized());

    private static async Task<IResult> ListTranslationsAsync(Guid organizationId, Guid campId,
        HttpContext context, IDevotionPlanning planning, CancellationToken cancellationToken) =>
        PlanningEndpointSupport.TryActor(context.User, out _)
            ? Results.Ok(await planning.ListBibleTranslationsAsync(cancellationToken))
            : Results.Unauthorized();

    private static async Task<IResult> GetAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IDevotionPlanning planning, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        var result = await planning.GetAsync(new DevotionKey(actorId, organizationId, campId, devotionId), cancellationToken);
        if (result is null) return Results.NotFound();
        PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
        return Results.Ok(result);
    });

    private static async Task<IResult> CreateAsync(Guid organizationId, Guid campId, DevotionBody body,
        HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.CreateAsync(new CreateDevotion(actorId, organizationId, campId, body.Topic,
                body.BibleReference, body.Translation, body.CoreMessage, body.MarkdownContent,
                body.ResponsibleUserIds, body.MaterialNotes, body.ScheduleEntryId), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/devotions/{result.Id:D}", result);
        });
    }

    private static async Task<IResult> UpdateAsync(Guid organizationId, Guid campId, Guid devotionId,
        DevotionBody body, HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.UpdateAsync(new UpdateDevotion(actorId, organizationId, campId, devotionId,
                body.Topic, body.BibleReference, body.Translation, body.CoreMessage, body.MarkdownContent,
                body.ResponsibleUserIds, body.MaterialNotes, body.ScheduleEntryId, version), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static Task<IResult> TrashAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning, CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(organizationId, campId, devotionId, context, antiforgery, planning.MoveToTrashAsync,
            StatusCodes.Status204NoContent, cancellationToken);

    private static Task<IResult> RestoreAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning, CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(organizationId, campId, devotionId, context, antiforgery, planning.RestoreAsync,
            StatusCodes.Status204NoContent, cancellationToken);

    private static async Task<IResult> ChangeLifecycleAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IAntiforgery antiforgery,
        Func<ChangeDevotionLifecycle, CancellationToken, Task> action,
        int statusCode, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            await action(new ChangeDevotionLifecycle(actorId, organizationId, campId, devotionId, version), cancellationToken);
            return Results.StatusCode(statusCode);
        });
    }

    private static async Task<IResult> RefreshSnapshotAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.RefreshBibleSnapshotAsync(
                new RefreshBibleSnapshot(actorId, organizationId, campId, devotionId, version), cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Devotion.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> SaveManualSnapshotAsync(Guid organizationId, Guid campId, Guid devotionId,
        ManualSnapshotBody body, HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.SaveManualBibleSnapshotAsync(new SaveManualBibleSnapshot(actorId,
                organizationId, campId, devotionId, body.Reference, body.Translation, body.TextExcerpt, version),
                cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (SpiritualRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message, "Andachtsplanung nicht möglich");
        }
    }

    private sealed record DevotionBody(
        string Topic,
        string BibleReference,
        BibleTranslation Translation,
        string CoreMessage,
        string MarkdownContent,
        IReadOnlyList<Guid> ResponsibleUserIds,
        string MaterialNotes,
        Guid? ScheduleEntryId);

    private sealed record ManualSnapshotBody(
        string Reference,
        BibleTranslation Translation,
        string TextExcerpt);
}

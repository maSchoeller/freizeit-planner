using Activity.Contracts;
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
        HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.CreateAsync(new CreateDevotion(actorId, organizationId, campId, body.Topic,
                body.BibleReference, body.Translation, body.CoreMessage, body.MarkdownContent,
                body.ResponsibleUserIds, body.MaterialNotes, body.ScheduleEntryId), cancellationToken);
            await UpsertActivityAsync(activity, actorId, result, ActivityKind.Created, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/devotions/{result.Id:D}", result);
        });
    }

    private static async Task<IResult> UpdateAsync(Guid organizationId, Guid campId, Guid devotionId,
        DevotionBody body, HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.UpdateAsync(new UpdateDevotion(actorId, organizationId, campId, devotionId,
                body.Topic, body.BibleReference, body.Translation, body.CoreMessage, body.MarkdownContent,
                body.ResponsibleUserIds, body.MaterialNotes, body.ScheduleEntryId, version), cancellationToken);
            await UpsertActivityAsync(activity, actorId, result, ActivityKind.Updated, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static Task<IResult> TrashAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(organizationId, campId, devotionId, context, antiforgery, planning.MoveToTrashAsync,
            planning, activity, false, cancellationToken);

    private static Task<IResult> RestoreAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(organizationId, campId, devotionId, context, antiforgery, planning.RestoreAsync,
            planning, activity, true, cancellationToken);

    private static async Task<IResult> ChangeLifecycleAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IAntiforgery antiforgery,
        Func<ChangeDevotionLifecycle, CancellationToken, Task> action,
        IDevotionPlanning planning, PlanningActivityWriter activity, bool restore,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            DevotionDetails? current = null;
            if (!restore)
            {
                current = await planning.GetAsync(
                    new DevotionKey(actorId, organizationId, campId, devotionId), cancellationToken);
                if (current is null) return Results.NotFound();
            }
            await action(new ChangeDevotionLifecycle(actorId, organizationId, campId, devotionId, version), cancellationToken);
            if (restore)
            {
                var restored = await planning.GetAsync(
                    new DevotionKey(actorId, organizationId, campId, devotionId), cancellationToken);
                if (restored is null) return Results.NotFound();
                await UpsertActivityAsync(activity, actorId, restored, ActivityKind.Restored, cancellationToken);
            }
            else
            {
                await activity.RemoveAsync(actorId, organizationId, campId, "Devotion", devotionId,
                    current!.Topic, version + 1, cancellationToken);
            }
            return Results.NoContent();
        });
    }

    private static async Task<IResult> RefreshSnapshotAsync(Guid organizationId, Guid campId, Guid devotionId,
        HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.RefreshBibleSnapshotAsync(
                new RefreshBibleSnapshot(actorId, organizationId, campId, devotionId, version), cancellationToken);
            await UpsertActivityAsync(activity, actorId, result.Devotion, ActivityKind.Updated, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Devotion.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> SaveManualSnapshotAsync(Guid organizationId, Guid campId, Guid devotionId,
        ManualSnapshotBody body, HttpContext context, IAntiforgery antiforgery, IDevotionPlanning planning,
        PlanningActivityWriter activity, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await planning.SaveManualBibleSnapshotAsync(new SaveManualBibleSnapshot(actorId,
                organizationId, campId, devotionId, body.Reference, body.Translation, body.TextExcerpt, version),
                cancellationToken);
            await UpsertActivityAsync(activity, actorId, result, ActivityKind.Updated, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static Task UpsertActivityAsync(PlanningActivityWriter activity, Guid actorId,
        DevotionDetails devotion, ActivityKind kind, CancellationToken cancellationToken) => activity.UpsertAsync(
        actorId, devotion.OrganizationId, devotion.CampId, kind, "Devotion", devotion.Id, devotion.Topic,
        string.Join(' ', devotion.Topic, devotion.BibleReference, devotion.CoreMessage,
            devotion.MarkdownContent, devotion.MaterialNotes),
        new Dictionary<string, string>
        {
            ["translation"] = devotion.Translation.ToString(),
            ["hasSnapshot"] = (devotion.BibleSnapshot is not null).ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        },
        devotion.Version,
        cancellationToken);

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (SpiritualRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message, "Andachtsplanung nicht möglich");
        }
        catch (ActivityRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message,
                "Aktivität konnte nicht gespeichert werden");
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

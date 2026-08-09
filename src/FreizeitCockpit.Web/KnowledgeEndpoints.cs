using Activity.Contracts;
using Knowledge.Contracts;
using Microsoft.AspNetCore.Antiforgery;

internal static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var notes = endpoints.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/camps/{campId:guid}/notes")
            .RequireAuthorization();
        notes.MapGet("/", ListAsync);
        notes.MapGet("/{noteId:guid}", GetAsync);
        notes.MapPost("/", CreateAsync);
        notes.MapPut("/{noteId:guid}", ReviseAsync);
        notes.MapDelete("/{noteId:guid}", TrashAsync);
        notes.MapPost("/{noteId:guid}/restore", RestoreAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(Guid organizationId, Guid campId, NotebookSection? section,
        string? tag, string? searchText, HttpContext context, ICampNotebook notebook,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await notebook.ListNotesAsync(new NotebookQuery(actorId, organizationId, campId,
                section ?? NotebookSection.Active, tag, searchText), cancellationToken))
            : Results.Unauthorized());

    private static async Task<IResult> GetAsync(Guid organizationId, Guid campId, Guid noteId,
        HttpContext context, ICampNotebook notebook, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        var result = await notebook.GetNoteAsync(new NoteRequest(actorId, organizationId, campId, noteId), cancellationToken);
        if (result is null) return Results.NotFound();
        PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
        return Results.Ok(result);
    });

    private static async Task<IResult> CreateAsync(Guid organizationId, Guid campId, NoteContent body,
        HttpContext context, IAntiforgery antiforgery, ICampNotebook notebook, PlanningActivityWriter activity,
        CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var result = await notebook.CreateNoteAsync(new CreateNote(actorId, organizationId, campId, body), cancellationToken);
            await UpsertActivityAsync(activity, actorId, result, ActivityKind.Created, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/notes/{result.Id:D}", result);
        });
    }

    private static async Task<IResult> ReviseAsync(Guid organizationId, Guid campId, Guid noteId,
        NoteContent body, HttpContext context, IAntiforgery antiforgery, ICampNotebook notebook,
        PlanningActivityWriter activity, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await notebook.ReviseNoteAsync(new ReviseNote(actorId, organizationId, campId,
                noteId, version, body), cancellationToken);
            await UpsertActivityAsync(activity, actorId, result, ActivityKind.Updated, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static Task<IResult> TrashAsync(Guid organizationId, Guid campId, Guid noteId,
        HttpContext context, IAntiforgery antiforgery, ICampNotebook notebook, PlanningActivityWriter activity,
        CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(context, antiforgery, activity, ActivityKind.Trashed, async (actorId, version) =>
            await notebook.MoveNoteToTrashAsync(new MoveNoteToTrash(actorId, organizationId, campId, noteId, version),
                cancellationToken));

    private static Task<IResult> RestoreAsync(Guid organizationId, Guid campId, Guid noteId,
        HttpContext context, IAntiforgery antiforgery, ICampNotebook notebook, PlanningActivityWriter activity,
        CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(context, antiforgery, activity, ActivityKind.Restored, async (actorId, version) =>
            await notebook.RestoreNoteAsync(new RestoreNote(actorId, organizationId, campId, noteId, version),
                cancellationToken));

    private static async Task<IResult> ChangeLifecycleAsync(HttpContext context, IAntiforgery antiforgery,
        PlanningActivityWriter activity, ActivityKind kind, Func<Guid, long, Task<Note>> action)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await action(actorId, version);
            if (kind == ActivityKind.Trashed)
            {
                await activity.RemoveAsync(actorId, result.OrganizationId, result.CampId, "Note", result.Id,
                    result.Title, result.Version, context.RequestAborted);
            }
            else
            {
                await UpsertActivityAsync(activity, actorId, result, kind, context.RequestAborted);
            }
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static Task UpsertActivityAsync(PlanningActivityWriter activity, Guid actorId, Note note,
        ActivityKind kind, CancellationToken cancellationToken) => activity.UpsertAsync(actorId,
        note.OrganizationId, note.CampId, kind, "Note", note.Id, note.Title,
        string.Join(' ', note.Title, note.Markdown, string.Join(' ', note.Tags),
            string.Join(' ', note.Links.Select(link => link.TargetTitle))),
        new Dictionary<string, string>
        {
            ["state"] = note.State.ToString(),
            ["pinned"] = note.IsPinned.ToString(System.Globalization.CultureInfo.InvariantCulture)
        },
        note.Version,
        cancellationToken);

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (KnowledgeRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message, "Notiz konnte nicht verarbeitet werden");
        }
        catch (ActivityRuleException exception)
        {
            return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message,
                "Aktivität konnte nicht gespeichert werden");
        }
    }
}

using Files.Contracts;
using Microsoft.AspNetCore.Antiforgery;

internal static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var campFiles = endpoints.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/camps/{campId:guid}/files")
            .RequireAuthorization();
        campFiles.MapGet("/", ListCampAsync);
        campFiles.MapPost("/", UploadCampAsync).DisableAntiforgery();
        campFiles.MapGet("/quota", GetCampQuotaAsync);
        campFiles.MapDelete("/{attachmentId:guid}", TrashCampAsync);
        campFiles.MapPost("/{attachmentId:guid}/restore", RestoreCampAsync);
        campFiles.MapPost("/{attachmentId:guid}/read-grant", IssueCampReadGrantAsync);
        campFiles.MapGet("/content", ReadAsync);

        var recipeFiles = endpoints.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/recipe-files")
            .RequireAuthorization();
        recipeFiles.MapGet("/", ListRecipeAsync);
        recipeFiles.MapPost("/", UploadRecipeAsync).DisableAntiforgery();
        recipeFiles.MapGet("/quota", GetRecipeQuotaAsync);
        recipeFiles.MapPost("/{attachmentId:guid}/read-grant", IssueRecipeReadGrantAsync);
        recipeFiles.MapGet("/content", ReadAsync);
        return endpoints;
    }

    private static Task<IResult> ListCampAsync(Guid organizationId, Guid campId, AttachmentOwnerType ownerType,
        Guid ownerId, bool? includeDeleted, HttpContext context, IAttachmentCatalog catalog,
        CancellationToken cancellationToken) => ListAsync(organizationId, campId, ownerType, ownerId,
            includeDeleted ?? false, context, catalog, cancellationToken);

    private static Task<IResult> ListRecipeAsync(Guid organizationId, Guid ownerId, bool? includeDeleted,
        HttpContext context, IAttachmentCatalog catalog, CancellationToken cancellationToken) =>
        ListAsync(organizationId, null, AttachmentOwnerType.Recipe, ownerId, includeDeleted ?? false,
            context, catalog, cancellationToken);

    private static async Task<IResult> ListAsync(Guid organizationId, Guid? campId, AttachmentOwnerType ownerType,
        Guid ownerId, bool includeDeleted, HttpContext context, IAttachmentCatalog catalog,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await catalog.ListAsync(new AttachmentOwnerQuery(actorId, organizationId, campId,
                new AttachmentOwnerReference(ownerType, ownerId), includeDeleted), cancellationToken))
            : Results.Unauthorized());

    private static Task<IResult> UploadCampAsync(Guid organizationId, Guid campId, AttachmentOwnerType ownerType,
        Guid ownerId, IFormFile file, HttpContext context, IAntiforgery antiforgery, IAttachmentCatalog catalog,
        CancellationToken cancellationToken) => UploadAsync(organizationId, campId, ownerType, ownerId, file,
            context, antiforgery, catalog, cancellationToken);

    private static Task<IResult> UploadRecipeAsync(Guid organizationId, Guid ownerId, IFormFile file,
        HttpContext context, IAntiforgery antiforgery, IAttachmentCatalog catalog,
        CancellationToken cancellationToken) => UploadAsync(organizationId, null, AttachmentOwnerType.Recipe,
            ownerId, file, context, antiforgery, catalog, cancellationToken);

    private static async Task<IResult> UploadAsync(Guid organizationId, Guid? campId,
        AttachmentOwnerType ownerType, Guid ownerId, IFormFile file, HttpContext context,
        IAntiforgery antiforgery, IAttachmentCatalog catalog, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            if (file.Length > AttachmentUploadLimit)
            {
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Datei zu groß", detail: "Eine Datei darf höchstens zehn MiB groß sein.",
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "attachment_too_large" });
            }
            await using var stream = file.OpenReadStream();
            var result = await catalog.UploadAsync(new UploadAttachment(actorId, organizationId, campId,
                new AttachmentOwnerReference(ownerType, ownerId), file.FileName, file.ContentType, file.Length),
                stream, cancellationToken);
            PlanningEndpointSupport.WriteEtag(context.Response, result.Version);
            return Results.Created($"{context.Request.Path}/{result.Id:D}", result);
        });
    }

    private static Task<IResult> GetCampQuotaAsync(Guid organizationId, Guid campId, HttpContext context,
        IAttachmentCatalog catalog, CancellationToken cancellationToken) => GetQuotaAsync(organizationId, campId,
            AttachmentQuotaScopeType.Camp, context, catalog, cancellationToken);

    private static Task<IResult> GetRecipeQuotaAsync(Guid organizationId, HttpContext context,
        IAttachmentCatalog catalog, CancellationToken cancellationToken) => GetQuotaAsync(organizationId, null,
            AttachmentQuotaScopeType.OrganizationRecipeLibrary, context, catalog, cancellationToken);

    private static async Task<IResult> GetQuotaAsync(Guid organizationId, Guid? campId,
        AttachmentQuotaScopeType scope, HttpContext context, IAttachmentCatalog catalog,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
        PlanningEndpointSupport.TryActor(context.User, out var actorId)
            ? Results.Ok(await catalog.GetQuotaAsync(new AttachmentQuotaQuery(actorId, organizationId, campId,
                scope), cancellationToken)) : Results.Unauthorized());

    private static Task<IResult> TrashCampAsync(Guid organizationId, Guid campId, Guid attachmentId,
        HttpContext context, IAntiforgery antiforgery, IAttachmentCatalog catalog,
        CancellationToken cancellationToken) => ChangeLifecycleAsync(organizationId, campId, attachmentId,
            context, antiforgery, catalog.MoveToTrashAsync, cancellationToken);

    private static Task<IResult> RestoreCampAsync(Guid organizationId, Guid campId, Guid attachmentId,
        HttpContext context, IAntiforgery antiforgery, IAttachmentCatalog catalog,
        CancellationToken cancellationToken) => ChangeLifecycleAsync(organizationId, campId, attachmentId,
            context, antiforgery, async (command, token) => { _ = await catalog.RestoreAsync(command, token); },
            cancellationToken);

    private static async Task<IResult> ChangeLifecycleAsync(Guid organizationId, Guid? campId, Guid attachmentId,
        HttpContext context, IAntiforgery antiforgery,
        Func<ChangeAttachmentLifecycle, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var version)) return PlanningEndpointSupport.PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            await action(new ChangeAttachmentLifecycle(actorId, organizationId,
            campId, attachmentId, version), cancellationToken); return Results.NoContent();
        });
    }

    private static Task<IResult> IssueCampReadGrantAsync(Guid organizationId, Guid campId, Guid attachmentId,
        HttpContext context, IAntiforgery antiforgery, IAttachmentReader reader,
        CancellationToken cancellationToken) => IssueReadGrantAsync(organizationId, campId, attachmentId,
            context, antiforgery, reader, cancellationToken);

    private static Task<IResult> IssueRecipeReadGrantAsync(Guid organizationId, Guid attachmentId,
        HttpContext context, IAntiforgery antiforgery, IAttachmentReader reader,
        CancellationToken cancellationToken) => IssueReadGrantAsync(organizationId, null, attachmentId,
            context, antiforgery, reader, cancellationToken);

    private static async Task<IResult> IssueReadGrantAsync(Guid organizationId, Guid? campId, Guid attachmentId,
        HttpContext context, IAntiforgery antiforgery, IAttachmentReader reader, CancellationToken cancellationToken)
    {
        if (await PlanningEndpointSupport.ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () => Results.Ok(await reader.IssueReadGrantAsync(
            new AttachmentReadGrantRequest(actorId, organizationId, campId, attachmentId), cancellationToken)));
    }

    private static async Task<IResult> ReadAsync(string token, HttpContext context, IAttachmentReader reader,
        CancellationToken cancellationToken)
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId)) return Results.Unauthorized();
        return await ExecuteAsync(async () =>
        {
            var content = await reader.OpenReadAsync(new OpenAttachmentReadGrant(actorId, token), cancellationToken);
            return Results.Stream(content.Content, content.ContentType,
                content.Disposition == AttachmentContentDisposition.Attachment ? content.FileName : null,
                enableRangeProcessing: true);
        });
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (FilesRuleException exception)
        { return PlanningEndpointSupport.Problem(exception.ErrorCode, exception.Message, "Dateioperation nicht möglich"); }
    }

    private const long AttachmentUploadLimit = 10L * 1024 * 1024;
}

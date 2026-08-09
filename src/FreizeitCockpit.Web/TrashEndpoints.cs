using Files.Contracts;
using Knowledge.Contracts;
using Logistics.Contracts;
using System.Security.Cryptography;
using System.Text;
using Spiritual.Contracts;

internal static class TrashEndpoints
{
    public static IEndpointRouteBuilder MapTrashEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/organizations/{organizationId:guid}/camps/{campId:guid}/trash",
                ListAsync)
            .RequireAuthorization()
            .Produces<CampTrashItem[]>();
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid organizationId,
        Guid campId,
        HttpContext context,
        ICampNotebook notebook,
        IDevotionPlanning devotions,
        IAttachmentCatalog attachments,
        IMaterialPlanning materials,
        CancellationToken cancellationToken)
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var result = new List<CampTrashItem>();
            var notes = await notebook.ListNotesAsync(
                new NotebookQuery(actorId, organizationId, campId, NotebookSection.Trash),
                cancellationToken);
            result.AddRange(notes
                .Where(item => item.TrashedAt is not null && item.PurgeAfter is not null)
                .Select(item => new CampTrashItem(
                    "Note",
                    item.Id,
                    item.Title,
                    item.TrashedAt!.Value,
                    item.PurgeAfter!.Value,
                    item.Version,
                    $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/notes/{item.Id:D}/restore")));

            var trashedDevotions = await devotions.ListTrashAsync(
                new DevotionScope(actorId, organizationId, campId),
                cancellationToken);
            result.AddRange(trashedDevotions.Select(item => new CampTrashItem(
                "Devotion",
                item.Id,
                item.Topic,
                item.DeletedAt,
                item.PurgeAt,
                item.Version,
                $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/devotions/{item.Id:D}/restore")));

            var trashedAttachments = await attachments.ListTrashAsync(
                new AttachmentTrashQuery(actorId, organizationId, campId),
                cancellationToken);
            result.AddRange(trashedAttachments
                .Where(item => item.DeletedAt is not null && item.PurgeAt is not null)
                .Select(item => new CampTrashItem(
                    "Attachment",
                    item.Id,
                    item.OriginalFileName,
                    item.DeletedAt!.Value,
                    item.PurgeAt!.Value,
                    item.Version,
                    $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/files/{item.Id:D}/restore")));

            var trashedMaterials = await materials.ListTrashAsync(
                new MaterialTrashQuery(actorId, organizationId, campId),
                cancellationToken);
            result.AddRange(trashedMaterials.Select(item => new CampTrashItem(
                "MaterialRequirement",
                item.Id,
                item.Name,
                item.DeletedAt,
                item.PurgeAt,
                item.Version,
                $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/logistics/material/{item.Id:D}/restore")));

            var ordered = result
                .OrderByDescending(item => item.DeletedAt)
                .ThenBy(item => item.ObjectType, StringComparer.Ordinal)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var etagSource = string.Join(
                '\n',
                ordered.Select(item => $"{item.ObjectType}:{item.ObjectId:D}:{item.Version}"));
            context.Response.Headers.ETag =
                $"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(etagSource)))}\"";
            return Results.Ok(ordered);
        }
        catch (KnowledgeRuleException exception)
        {
            return PlanningEndpointSupport.Problem(
                exception.ErrorCode,
                exception.Message,
                "Papierkorb konnte nicht geladen werden");
        }
        catch (SpiritualRuleException exception)
        {
            return PlanningEndpointSupport.Problem(
                exception.ErrorCode,
                exception.Message,
                "Papierkorb konnte nicht geladen werden");
        }
        catch (FilesRuleException exception)
        {
            return PlanningEndpointSupport.Problem(
                exception.ErrorCode,
                exception.Message,
                "Papierkorb konnte nicht geladen werden");
        }
        catch (LogisticsRuleException exception)
        {
            return PlanningEndpointSupport.Problem(
                exception.ErrorCode,
                exception.Message,
                "Papierkorb konnte nicht geladen werden");
        }
    }
}

internal sealed record CampTrashItem(
    string ObjectType,
    Guid ObjectId,
    string Title,
    DateTimeOffset DeletedAt,
    DateTimeOffset PurgeAt,
    long Version,
    string RestorePath);

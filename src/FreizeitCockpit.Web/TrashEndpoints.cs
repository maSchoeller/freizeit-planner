using Files.Contracts;
using Camps.Contracts;
using Catering.Contracts;
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
        IShoppingPlanning shopping,
        ISchedulePlanning schedule,
        ICampMealPlanning meals,
        CancellationToken cancellationToken)
    {
        if (!PlanningEndpointSupport.TryActor(context.User, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var result = new List<CampTrashItem>();
            var trashedScheduleEntries = await schedule.ListTrashAsync(
                new ScheduleTrashQuery(actorId, organizationId, campId),
                cancellationToken);
            result.AddRange(trashedScheduleEntries.Select(item => new CampTrashItem(
                "ScheduleEntry",
                item.Id,
                item.Title,
                item.DeletedAt,
                item.PurgeAt,
                item.Version,
                $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/schedule/{item.Id:D}/restore")));

            var trashedMeals = await meals.ListMealTrashAsync(
                new MealTrashQuery(actorId, organizationId, campId),
                cancellationToken);
            result.AddRange(trashedMeals.Select(item => new CampTrashItem(
                "Meal",
                item.Id,
                item.Name,
                item.DeletedAt,
                item.PurgeAt,
                item.Version,
                $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/catering/meals/{item.Id:D}/restore")));

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

            var trashedShoppingLists = await shopping.ListTrashAsync(
                new ShoppingTrashQuery(actorId, organizationId, campId),
                cancellationToken);
            result.AddRange(trashedShoppingLists.Select(item => new CampTrashItem(
                "ShoppingList",
                item.Id,
                item.Name,
                item.DeletedAt,
                item.PurgeAt,
                item.Version,
                $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/logistics/shopping-lists/{item.Id:D}/restore")));

            var trashedShoppingItems = await shopping.ListItemTrashAsync(
                new ShoppingItemTrashQuery(actorId, organizationId, campId),
                cancellationToken);
            result.AddRange(trashedShoppingItems.Select(item => new CampTrashItem(
                "ShoppingItem",
                item.Id,
                item.Name,
                item.DeletedAt,
                item.PurgeAt,
                item.Version,
                $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/logistics/shopping-lists/{item.ShoppingListId:D}/items/{item.Id:D}/restore")));

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
        catch (CampsRuleException exception)
        {
            return PlanningEndpointSupport.Problem(
                exception.ErrorCode,
                exception.Message,
                "Papierkorb konnte nicht geladen werden");
        }
        catch (CateringRuleException exception)
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

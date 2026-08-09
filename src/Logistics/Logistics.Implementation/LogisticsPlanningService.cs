using Camps.Contracts;
using Identity.Contracts;
using Logistics.Contracts;

namespace Logistics.Implementation;

public sealed class LogisticsPlanningService(
    ILogisticsState state,
    ITenantAccessControl accessControl,
    ICampPlanningDefaults campDefaults,
    IScheduleReferenceAccess scheduleReferences,
    TimeProvider timeProvider) : IMaterialPlanning, IShoppingPlanning, IShoppingTransfer, IShoppingAudit
{
    public async Task<IReadOnlyList<MaterialRequirementSummary>> ListAsync(MaterialQuery query, CancellationToken cancellationToken)
    {
        await RequireReadAsync(query.ActorId, query.OrganizationId, query.CampId, cancellationToken);
        return (await state.ListMaterialsAsync(query.OrganizationId, query.CampId, cancellationToken))
            .Where(item => (query.ScheduleEntryId is null || item.ScheduleEntryId == query.ScheduleEntryId)
                && (query.Status is null || item.Status == query.Status))
            .OrderBy(item => item.Name, StringComparer.CurrentCulture)
            .Select(ToSummary)
            .ToArray();
    }

    public async Task<MaterialRequirement?> GetAsync(MaterialRequest request, CancellationToken cancellationToken)
    {
        await RequireReadAsync(request.ActorId, request.OrganizationId, request.CampId, cancellationToken);
        var record = await state.FindMaterialAsync(request.OrganizationId, request.CampId, request.MaterialRequirementId, cancellationToken);
        return record is null ? null : ToView(record);
    }

    public async Task<MaterialRequirement> CreateAsync(CreateMaterialRequirement command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var values = ValidateMaterial(command.Name, command.Description, command.ResponsibleUserIds, command.ProcurementSource, command.Note);
        await ValidateResponsibilitiesAsync(command.OrganizationId, command.CampId, values.ResponsibleUserIds, cancellationToken);
        await ValidateScheduleAsync(command.ActorId, command.OrganizationId, command.CampId, command.ScheduleEntryId, cancellationToken);
        var record = new MaterialRequirementRecord(Guid.NewGuid(), command.OrganizationId, command.CampId, values.Name, values.Description, command.Quantity, values.ResponsibleUserIds, values.ProcurementSource, values.Note, command.Status, command.ScheduleEntryId);
        await state.AddMaterialAsync(record, cancellationToken);
        return ToView(record);
    }

    public async Task<MaterialRequirement> UpdateAsync(UpdateMaterialRequirement command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var record = await RequireMaterialAsync(command.OrganizationId, command.CampId, command.MaterialRequirementId, cancellationToken);
        var values = ValidateMaterial(command.Name, command.Description, command.ResponsibleUserIds, command.ProcurementSource, command.Note);
        await ValidateResponsibilitiesAsync(command.OrganizationId, command.CampId, values.ResponsibleUserIds, cancellationToken);
        await ValidateScheduleAsync(command.ActorId, command.OrganizationId, command.CampId, command.ScheduleEntryId, cancellationToken);
        record.Update(values.Name, values.Description, command.Quantity, values.ResponsibleUserIds, values.ProcurementSource, values.Note, command.Status, command.ScheduleEntryId, command.ExpectedVersion);
        await state.SaveMaterialAsync(record, command.ExpectedVersion, cancellationToken);
        return ToView(record);
    }

    public async Task DeleteAsync(DeleteMaterialRequirement command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var record = await RequireMaterialAsync(command.OrganizationId, command.CampId, command.MaterialRequirementId, cancellationToken);
        record.MoveToTrash(command.ExpectedVersion, timeProvider.GetUtcNow());
        await state.SaveMaterialAsync(record, command.ExpectedVersion, cancellationToken);
    }

    public async Task<IReadOnlyList<TrashedMaterialRequirement>> ListTrashAsync(
        MaterialTrashQuery query,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(
            query.ActorId,
            query.OrganizationId,
            query.CampId,
            CampAction.ManageCamp,
            false,
            cancellationToken);
        return (await state.ListDeletedMaterialsAsync(query.OrganizationId, query.CampId, cancellationToken))
            .OrderByDescending(item => item.DeletedAt)
            .Select(item => new TrashedMaterialRequirement(
                item.Id,
                item.OrganizationId,
                item.CampId,
                item.Name,
                item.DeletedAt!.Value,
                item.PurgeAt!.Value,
                item.Version))
            .ToArray();
    }

    public async Task<MaterialRequirement> RestoreAsync(
        RestoreMaterialRequirement command,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.ManageCamp,
            true,
            cancellationToken);
        var record = await state.FindDeletedMaterialAsync(
            command.OrganizationId,
            command.CampId,
            command.MaterialRequirementId,
            cancellationToken)
            ?? throw Rule("material_not_found", "Der Materialbedarf wurde nicht gefunden.");
        record.Restore(command.ExpectedVersion, timeProvider.GetUtcNow());
        await state.SaveMaterialAsync(record, command.ExpectedVersion, cancellationToken);
        return ToView(record);
    }

    public async Task<IReadOnlyList<ShoppingListSummary>> ListAsync(ShoppingListsQuery query, CancellationToken cancellationToken)
    {
        await RequireReadAsync(query.ActorId, query.OrganizationId, query.CampId, cancellationToken);
        return (await state.ListShoppingListsAsync(query.OrganizationId, query.CampId, cancellationToken))
            .OrderBy(item => item.Name, StringComparer.CurrentCulture)
            .Select(ToSummary)
            .ToArray();
    }

    public async Task<ShoppingList?> GetAsync(ShoppingListRequest request, CancellationToken cancellationToken)
    {
        await RequireReadAsync(request.ActorId, request.OrganizationId, request.CampId, cancellationToken);
        var list = await state.FindShoppingListAsync(request.OrganizationId, request.CampId, request.ShoppingListId, cancellationToken);
        return list is null ? null : ToView(list);
    }

    public async Task<ShoppingList> CreateListAsync(CreateShoppingList command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var list = new ShoppingListRecord(Guid.NewGuid(), command.OrganizationId, command.CampId, Required(command.Name, 160, "invalid_list_name", "Der Listenname ist ungültig."));
        await state.AddShoppingListAsync(list, cancellationToken);
        return ToView(list);
    }

    public async Task<ShoppingList> RenameListAsync(RenameShoppingList command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var list = await RequireListAsync(command.OrganizationId, command.CampId, command.ShoppingListId, cancellationToken);
        list.Rename(Required(command.Name, 160, "invalid_list_name", "Der Listenname ist ungültig."), command.ExpectedListVersion);
        await state.SaveShoppingListAsync(list, command.ExpectedListVersion, cancellationToken);
        return ToView(list);
    }

    public async Task DeleteListAsync(DeleteShoppingList command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var list = await RequireListAsync(command.OrganizationId, command.CampId, command.ShoppingListId, cancellationToken);
        list.RequireVersion(command.ExpectedListVersion);
        await state.DeleteShoppingListAsync(list, command.ExpectedListVersion, cancellationToken);
    }

    public async Task<ShoppingListChange> AddSpontaneousItemAsync(AddSpontaneousShoppingItem command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var list = await RequireListAsync(command.OrganizationId, command.CampId, command.ShoppingListId, cancellationToken);
        var content = await ValidateContentAsync(command.OrganizationId, command.CampId, command.Content, cancellationToken);
        var item = NewItem(list, content, new ShoppingItemSource(ShoppingSourceKind.Spontaneous, "Spontan"));
        list.AddItems([item], command.ExpectedListVersion);
        await state.AddShoppingItemsAsync(list, [item], command.ExpectedListVersion, cancellationToken);
        return Change(list, item);
    }

    public async Task<ShoppingListChange> UpdateItemAsync(UpdateShoppingItem command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var list = await RequireListAsync(command.OrganizationId, command.CampId, command.ShoppingListId, cancellationToken);
        var item = RequireItem(list, command.ShoppingItemId);
        var content = await ValidateContentAsync(command.OrganizationId, command.CampId, command.Content, cancellationToken);
        item.Update(content, command.ExpectedItemVersion);
        list.AdvanceItemChange();
        await state.SaveShoppingItemAsync(list, item, command.ExpectedItemVersion, null, cancellationToken);
        return Change(list, item);
    }

    public async Task<ShoppingListChange> SetItemCheckedAsync(SetShoppingItemChecked command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var list = await RequireListAsync(command.OrganizationId, command.CampId, command.ShoppingListId, cancellationToken);
        var item = RequireItem(list, command.ShoppingItemId);
        var audit = item.SetChecked(command.IsChecked, command.ActorId, timeProvider.GetUtcNow(), command.ExpectedItemVersion);
        list.AdvanceItemChange();
        await state.SaveShoppingItemAsync(list, item, command.ExpectedItemVersion, audit, cancellationToken);
        return Change(list, item);
    }

    public async Task<ShoppingListChange> DeleteItemAsync(DeleteShoppingItem command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var list = await RequireListAsync(command.OrganizationId, command.CampId, command.ShoppingListId, cancellationToken);
        var item = RequireItem(list, command.ShoppingItemId);
        item.RequireVersion(command.ExpectedItemVersion);
        list.RemoveItem(item);
        await state.DeleteShoppingItemAsync(list, item, command.ExpectedItemVersion, cancellationToken);
        return Change(list, null);
    }

    public async Task<ShoppingTransferResult> TransferCateringAsync(TransferCateringShoppingItems command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        if (command.Lines.Count == 0) throw Rule("transfer_empty", "Wähle mindestens eine Position für die Übernahme aus.");
        var list = await RequireListAsync(command.OrganizationId, command.CampId, command.ShoppingListId, cancellationToken);
        var items = new List<ShoppingItemRecord>(command.Lines.Count);
        foreach (var line in command.Lines)
        {
            ValidateCateringSource(line.Source);
            var content = await ValidateContentAsync(command.OrganizationId, command.CampId, line.Content, cancellationToken);
            var source = new ShoppingItemSource(ShoppingSourceKind.Catering, Required(line.SourceLabel, 240, "invalid_source", "Die Quellenangabe ist ungültig."), line.Source);
            items.Add(NewItem(list, content, source));
        }
        list.AddItems(items, command.ExpectedListVersion);
        await state.AddShoppingItemsAsync(list, items, command.ExpectedListVersion, cancellationToken);
        return TransferResult(list, items);
    }

    public async Task<ShoppingTransferResult> TransferMaterialAsync(TransferMaterialRequirement command, CancellationToken cancellationToken)
    {
        await RequireWriteAsync(command.ActorId, command.OrganizationId, command.CampId, cancellationToken);
        var material = await RequireMaterialAsync(command.OrganizationId, command.CampId, command.MaterialRequirementId, cancellationToken);
        material.RequireVersion(command.ExpectedRequirementVersion);
        var list = await RequireListAsync(command.OrganizationId, command.CampId, command.ShoppingListId, cancellationToken);
        var content = await ValidateContentAsync(command.OrganizationId, command.CampId, command.Content, cancellationToken);
        var source = new ShoppingItemSource(ShoppingSourceKind.MaterialRequirement, material.Name, null, new MaterialSourceReference(material.Id, material.Version));
        var item = NewItem(list, content, source);
        list.AddItems([item], command.ExpectedListVersion);
        await state.AddShoppingItemsAsync(list, [item], command.ExpectedListVersion, cancellationToken);
        return TransferResult(list, [item]);
    }

    public async Task<IReadOnlyList<ShoppingCheckEvent>> ListCheckEventsAsync(ShoppingCheckAuditQuery query, CancellationToken cancellationToken)
    {
        await RequireReadAsync(query.ActorId, query.OrganizationId, query.CampId, cancellationToken);
        _ = RequireItem(await RequireListAsync(query.OrganizationId, query.CampId, query.ShoppingListId, cancellationToken), query.ShoppingItemId);
        return (await state.ListCheckEventsAsync(query.OrganizationId, query.CampId, query.ShoppingListId, query.ShoppingItemId, cancellationToken))
            .OrderBy(item => item.OccurredAt)
            .Select(item => new ShoppingCheckEvent(item.Id, item.ShoppingItemId, item.Action, item.ActorId, item.OccurredAt, item.ResultingItemVersion))
            .ToArray();
    }

    private async Task RequireReadAsync(Guid actorId, Guid organizationId, Guid campId, CancellationToken cancellationToken) => await RequireAccessAsync(actorId, organizationId, campId, CampAction.Read, false, cancellationToken);
    private async Task RequireWriteAsync(Guid actorId, Guid organizationId, Guid campId, CancellationToken cancellationToken) => await RequireAccessAsync(actorId, organizationId, campId, CampAction.WriteContent, true, cancellationToken);

    private async Task RequireAccessAsync(Guid actorId, Guid organizationId, Guid campId, CampAction action, bool requireActive, CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeCampAsync(new CampAccessRequest(actorId, organizationId, campId, action), cancellationToken);
        if (!decision.Allowed) throw Rule("access_denied", "Du darfst diese Planung nicht verwenden.");
        if (requireActive)
        {
            var defaults = await campDefaults.GetAsync(new CampAccessQuery(actorId, organizationId, campId), cancellationToken);
            if (defaults.Status == CampStatus.Archived) throw Rule("camp_archived", "Archivierte Camps sind schreibgeschützt.");
        }
    }

    private async Task ValidateScheduleAsync(Guid actorId, Guid organizationId, Guid campId, Guid? scheduleEntryId, CancellationToken cancellationToken)
    {
        if (scheduleEntryId is null) return;
        _ = await scheduleReferences.RequireAsync(new ScheduleEntryReferenceRequest(actorId, organizationId, campId, scheduleEntryId.Value, ScheduleReferencePurpose.LinkForWrite), cancellationToken);
    }

    private async Task ValidateResponsibilitiesAsync(Guid organizationId, Guid campId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            var decision = await accessControl.AuthorizeCampAsync(new CampAccessRequest(userId, organizationId, campId, CampAction.Read), cancellationToken);
            if (!decision.Allowed) throw Rule("invalid_responsibility", "Mindestens eine verantwortliche Person hat keinen Zugriff auf dieses Camp.");
        }
    }

    private async Task<ShoppingItemContent> ValidateContentAsync(Guid organizationId, Guid campId, ShoppingItemContent content, CancellationToken cancellationToken)
    {
        var users = content.ResponsibleUserIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (users.Length != content.ResponsibleUserIds.Count) throw Rule("invalid_responsibility", "Verantwortliche Personen dürfen nicht doppelt vorkommen.");
        await ValidateResponsibilitiesAsync(organizationId, campId, users, cancellationToken);
        return new ShoppingItemContent(Required(content.Name, 200, "invalid_item_name", "Die Bezeichnung ist ungültig."), content.Quantity, users, Optional(content.Store, 160, "Das Geschäft ist zu lang."), Optional(content.Note, 2000, "Die Notiz ist zu lang."));
    }

    private static MaterialValues ValidateMaterial(string name, string? description, IReadOnlyList<Guid> responsibleUserIds, string? source, string? note)
    {
        var users = responsibleUserIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (users.Length == 0 || users.Length != responsibleUserIds.Count) throw Rule("invalid_responsibility", "Wähle mindestens eine eindeutige verantwortliche Person.");
        return new MaterialValues(Required(name, 200, "invalid_material_name", "Die Bezeichnung ist ungültig."), Optional(description, 4000, "Die Beschreibung ist zu lang."), users, Optional(source, 240, "Die Beschaffungsquelle ist zu lang."), Optional(note, 2000, "Die Notiz ist zu lang."));
    }

    private static string Required(string value, int max, string code, string message)
    {
        var normalized = value.Trim();
        return normalized.Length is > 0 && normalized.Length <= max ? normalized : throw Rule(code, message);
    }

    private static string? Optional(string? value, int max, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= max ? normalized : throw Rule("invalid_content", message);
    }

    private static void ValidateCateringSource(CateringSourceReference source)
    {
        if (source.MealId == Guid.Empty || source.RecipeSnapshotId == Guid.Empty || source.SnapshotIngredientId == Guid.Empty || source.SourceRecipeId == Guid.Empty || source.SourceRecipeVersionNumber <= 0)
            throw Rule("invalid_source", "Die Catering-Quellenangabe ist unvollständig.");
    }

    private static ShoppingItemRecord NewItem(ShoppingListRecord list, ShoppingItemContent content, ShoppingItemSource source) => new(Guid.NewGuid(), list.OrganizationId, list.CampId, list.Id, content, source);
    private async Task<MaterialRequirementRecord> RequireMaterialAsync(Guid organizationId, Guid campId, Guid id, CancellationToken ct) => await state.FindMaterialAsync(organizationId, campId, id, ct) ?? throw Rule("material_not_found", "Der Materialbedarf wurde nicht gefunden.");
    private async Task<ShoppingListRecord> RequireListAsync(Guid organizationId, Guid campId, Guid id, CancellationToken ct) => await state.FindShoppingListAsync(organizationId, campId, id, ct) ?? throw Rule("shopping_list_not_found", "Die Einkaufsliste wurde nicht gefunden.");
    private static ShoppingItemRecord RequireItem(ShoppingListRecord list, Guid id) => list.Items.SingleOrDefault(item => item.Id == id) ?? throw Rule("shopping_item_not_found", "Die Einkaufsposition wurde nicht gefunden.");

    private static MaterialRequirement ToView(MaterialRequirementRecord x) => new(x.Id, x.OrganizationId, x.CampId, x.Name, x.Description, x.Quantity, x.ResponsibleUserIds, x.ProcurementSource, x.Note, x.Status, x.ScheduleEntryId, x.Version);
    private static MaterialRequirementSummary ToSummary(MaterialRequirementRecord x) => new(x.Id, x.OrganizationId, x.CampId, x.Name, x.Quantity, x.Status, x.ScheduleEntryId, x.Version);
    private static ShoppingList ToView(ShoppingListRecord x) => new(x.Id, x.OrganizationId, x.CampId, x.Name, x.Items.Select(ToView).ToArray(), x.Version, x.ChangeSequence);
    private static ShoppingItem ToView(ShoppingItemRecord x) => new(x.Id, x.ShoppingListId, x.Name, x.Quantity, x.ResponsibleUserIds, x.Store, x.Note, x.Source, x.IsChecked, x.CheckedByUserId, x.CheckedAt, x.Version);
    private static ShoppingListSummary ToSummary(ShoppingListRecord x) => new(x.Id, x.Name, x.Items.Count(item => !item.IsChecked), x.Items.Count(item => item.IsChecked), x.Version, x.ChangeSequence);
    private static ShoppingListChange Change(ShoppingListRecord list, ShoppingItemRecord? item) => new(list.Id, list.Version, list.ChangeSequence, item is null ? null : ToView(item));
    private static ShoppingTransferResult TransferResult(ShoppingListRecord list, IReadOnlyList<ShoppingItemRecord> items) => new(list.Id, list.Version, list.ChangeSequence, items.Select(ToView).ToArray());
    private static LogisticsRuleException Rule(string code, string message) => new(code, message);
    private sealed record MaterialValues(string Name, string? Description, IReadOnlyList<Guid> ResponsibleUserIds, string? ProcurementSource, string? Note);
}

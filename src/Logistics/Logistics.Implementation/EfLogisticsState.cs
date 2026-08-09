using Logistics.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Logistics.Implementation;

public sealed class EfLogisticsState(LogisticsDbContext dbContext) : ILogisticsState
{
    public async ValueTask<IReadOnlyList<MaterialRequirementRecord>> ListMaterialsAsync(Guid organizationId, Guid campId, CancellationToken cancellationToken)
    {
        var entities = await dbContext.Materials.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.DeletedAt == null).ToArrayAsync(cancellationToken);
        var ids = entities.Select(x => x.Id).ToArray();
        var responsibilities = await dbContext.MaterialResponsibilities.AsNoTracking().Where(x => ids.Contains(x.MaterialRequirementId)).ToArrayAsync(cancellationToken);
        return entities.Select(x => ToRecord(x, responsibilities.Where(r => r.MaterialRequirementId == x.Id).Select(r => r.UserId).ToArray())).ToArray();
    }

    public async ValueTask<MaterialRequirementRecord?> FindMaterialAsync(Guid organizationId, Guid campId, Guid materialId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Materials.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.CampId == campId && x.Id == materialId && x.DeletedAt == null, cancellationToken);
        if (entity is null) return null;
        var users = await dbContext.MaterialResponsibilities.AsNoTracking().Where(x => x.MaterialRequirementId == materialId).Select(x => x.UserId).ToArrayAsync(cancellationToken);
        return ToRecord(entity, users);
    }

    public async ValueTask AddMaterialAsync(MaterialRequirementRecord material, CancellationToken cancellationToken)
    {
        dbContext.Materials.Add(ToEntity(material));
        dbContext.MaterialResponsibilities.AddRange(MaterialResponsibilities(material));
        await SaveAsync(cancellationToken);
    }

    public async ValueTask SaveMaterialAsync(MaterialRequirementRecord material, long expectedVersion, CancellationToken cancellationToken)
    {
        var entity = ToEntity(material);
        dbContext.Materials.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        dbContext.Entry(entity).Property(x => x.Version).OriginalValue = expectedVersion;
        await SynchronizeMaterialResponsibilitiesAsync(material, cancellationToken);
        await SaveAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<MaterialRequirementRecord>> ListDeletedMaterialsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var entities = await dbContext.Materials
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.DeletedAt != null)
            .ToArrayAsync(cancellationToken);
        var ids = entities.Select(x => x.Id).ToArray();
        var responsibilities = await dbContext.MaterialResponsibilities
            .AsNoTracking()
            .Where(x => ids.Contains(x.MaterialRequirementId))
            .ToArrayAsync(cancellationToken);
        return entities.Select(x => ToRecord(
            x,
            responsibilities.Where(r => r.MaterialRequirementId == x.Id).Select(r => r.UserId).ToArray()))
            .ToArray();
    }

    public async ValueTask<MaterialRequirementRecord?> FindDeletedMaterialAsync(
        Guid organizationId,
        Guid campId,
        Guid materialId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Materials.AsNoTracking().SingleOrDefaultAsync(
            x => x.OrganizationId == organizationId
                && x.CampId == campId
                && x.Id == materialId
                && x.DeletedAt != null,
            cancellationToken);
        if (entity is null) return null;
        var users = await dbContext.MaterialResponsibilities.AsNoTracking()
            .Where(x => x.MaterialRequirementId == materialId)
            .Select(x => x.UserId)
            .ToArrayAsync(cancellationToken);
        return ToRecord(entity, users);
    }

    public async ValueTask<int> PurgeDueMaterialsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.Materials
            .Where(x => x.PurgeAt != null && x.PurgeAt <= now)
            .OrderBy(x => x.PurgeAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        return ids.Length == 0
            ? 0
            : await dbContext.Materials.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<ShoppingListRecord>> ListShoppingListsAsync(Guid organizationId, Guid campId, CancellationToken cancellationToken)
    {
        var lists = await dbContext.ShoppingLists.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.DeletedAt == null).ToArrayAsync(cancellationToken);
        var result = new List<ShoppingListRecord>(lists.Length);
        foreach (var list in lists) result.Add(await ToRecordAsync(list, cancellationToken));
        return result;
    }

    public async ValueTask<ShoppingListRecord?> FindShoppingListAsync(Guid organizationId, Guid campId, Guid shoppingListId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ShoppingLists.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.CampId == campId && x.Id == shoppingListId && x.DeletedAt == null, cancellationToken);
        return entity is null ? null : await ToRecordAsync(entity, cancellationToken);
    }

    public async ValueTask AddShoppingListAsync(ShoppingListRecord list, CancellationToken cancellationToken)
    {
        dbContext.ShoppingLists.Add(ToEntity(list));
        await SaveAsync(cancellationToken);
    }

    public async ValueTask SaveShoppingListAsync(ShoppingListRecord list, long expectedVersion, CancellationToken cancellationToken)
    {
        var entity = ToEntity(list);
        dbContext.ShoppingLists.Attach(entity);
        dbContext.Entry(entity).State = EntityState.Modified;
        dbContext.Entry(entity).Property(x => x.Version).OriginalValue = expectedVersion;
        await SaveAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<ShoppingListRecord>> ListDeletedShoppingListsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var entities = await dbContext.ShoppingLists.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.DeletedAt != null)
            .ToArrayAsync(cancellationToken);
        var result = new List<ShoppingListRecord>(entities.Length);
        foreach (var entity in entities) result.Add(await ToRecordAsync(entity, cancellationToken));
        return result;
    }

    public async ValueTask<ShoppingListRecord?> FindDeletedShoppingListAsync(
        Guid organizationId,
        Guid campId,
        Guid shoppingListId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ShoppingLists.AsNoTracking().SingleOrDefaultAsync(
            x => x.OrganizationId == organizationId && x.CampId == campId
                && x.Id == shoppingListId && x.DeletedAt != null,
            cancellationToken);
        return entity is null ? null : await ToRecordAsync(entity, cancellationToken);
    }

    public async ValueTask<int> PurgeDueShoppingListsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.ShoppingLists.Where(x => x.PurgeAt != null && x.PurgeAt <= now)
            .OrderBy(x => x.PurgeAt).ThenBy(x => x.Id).Select(x => x.Id).Take(batchSize)
            .ToArrayAsync(cancellationToken);
        if (ids.Length == 0) return 0;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.ShoppingCheckEvents.Where(x => ids.Contains(x.ShoppingListId))
            .ExecuteDeleteAsync(cancellationToken);
        var deleted = await dbContext.ShoppingLists.Where(x => ids.Contains(x.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    public async ValueTask AddShoppingItemsAsync(ShoppingListRecord list, IReadOnlyList<ShoppingItemRecord> items, long expectedListVersion, CancellationToken cancellationToken)
    {
        var listEntity = ToEntity(list);
        dbContext.ShoppingLists.Attach(listEntity);
        dbContext.Entry(listEntity).State = EntityState.Modified;
        dbContext.Entry(listEntity).Property(x => x.Version).OriginalValue = expectedListVersion;
        dbContext.ShoppingItems.AddRange(items.Select(ToEntity));
        dbContext.ShoppingItemResponsibilities.AddRange(items.SelectMany(ItemResponsibilities));
        await SaveAsync(cancellationToken);
    }

    public async ValueTask SaveShoppingItemAsync(ShoppingListRecord list, ShoppingItemRecord item, long expectedItemVersion, ShoppingCheckEventRecord? auditEvent, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginIfNeededAsync(cancellationToken);
        try
        {
            var entity = ToEntity(item);
            dbContext.ShoppingItems.Attach(entity);
            dbContext.Entry(entity).State = EntityState.Modified;
            dbContext.Entry(entity).Property(x => x.Version).OriginalValue = expectedItemVersion;
            await SynchronizeItemResponsibilitiesAsync(item, cancellationToken);
            if (auditEvent is not null) dbContext.ShoppingCheckEvents.Add(ToEntity(auditEvent));
            await SaveAsync(cancellationToken);
            await AdvanceListAsync(list, false, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async ValueTask DeleteShoppingItemAsync(ShoppingListRecord list, ShoppingItemRecord item, long expectedItemVersion, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginIfNeededAsync(cancellationToken);
        try
        {
            var entity = ToEntity(item);
            dbContext.ShoppingItems.Attach(entity);
            dbContext.Entry(entity).Property(x => x.Version).OriginalValue = expectedItemVersion;
            dbContext.ShoppingItems.Remove(entity);
            await SaveAsync(cancellationToken);
            await AdvanceListAsync(list, true, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async ValueTask<IReadOnlyList<ShoppingCheckEventRecord>> ListCheckEventsAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId, CancellationToken cancellationToken) =>
        (await dbContext.ShoppingCheckEvents.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.ShoppingListId == listId && x.ShoppingItemId == itemId).ToArrayAsync(cancellationToken))
            .Select(ToRecord).ToArray();

    private async Task SynchronizeMaterialResponsibilitiesAsync(MaterialRequirementRecord material, CancellationToken cancellationToken)
    {
        var current = await dbContext.MaterialResponsibilities.Where(x => x.MaterialRequirementId == material.Id).ToArrayAsync(cancellationToken);
        var requested = material.ResponsibleUserIds.ToHashSet();
        dbContext.MaterialResponsibilities.RemoveRange(current.Where(x => !requested.Contains(x.UserId)));
        var existing = current.Select(x => x.UserId).ToHashSet();
        dbContext.MaterialResponsibilities.AddRange(MaterialResponsibilities(material).Where(x => !existing.Contains(x.UserId)));
    }

    private async Task SynchronizeItemResponsibilitiesAsync(ShoppingItemRecord item, CancellationToken cancellationToken)
    {
        var current = await dbContext.ShoppingItemResponsibilities.Where(x => x.ShoppingItemId == item.Id).ToArrayAsync(cancellationToken);
        var requested = item.ResponsibleUserIds.ToHashSet();
        dbContext.ShoppingItemResponsibilities.RemoveRange(current.Where(x => !requested.Contains(x.UserId)));
        var existing = current.Select(x => x.UserId).ToHashSet();
        dbContext.ShoppingItemResponsibilities.AddRange(ItemResponsibilities(item).Where(x => !existing.Contains(x.UserId)));
    }

    private async Task AdvanceListAsync(ShoppingListRecord list, bool structural, CancellationToken cancellationToken)
    {
        var affected = structural
            ? await dbContext.ShoppingLists.Where(x => x.Id == list.Id && x.OrganizationId == list.OrganizationId && x.CampId == list.CampId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.ChangeSequence, x => x.ChangeSequence + 1), cancellationToken)
            : await dbContext.ShoppingLists.Where(x => x.Id == list.Id && x.OrganizationId == list.OrganizationId && x.CampId == list.CampId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ChangeSequence, x => x.ChangeSequence + 1), cancellationToken);
        if (affected != 1) throw Rule("shopping_list_not_found", "Die Einkaufsliste wurde nicht gefunden.");
        var revision = await dbContext.ShoppingLists.AsNoTracking().Where(x => x.Id == list.Id).Select(x => new { x.Version, x.ChangeSequence }).SingleAsync(cancellationToken);
        list.SynchronizeRevision(revision.Version, revision.ChangeSequence);
    }

    private async ValueTask<IDbContextTransaction?> BeginIfNeededAsync(CancellationToken cancellationToken) =>
        dbContext.Database.CurrentTransaction is null ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;

    private async Task<ShoppingListRecord> ToRecordAsync(ShoppingListEntity entity, CancellationToken cancellationToken)
    {
        var items = await dbContext.ShoppingItems.AsNoTracking().Where(x => x.ShoppingListId == entity.Id && x.OrganizationId == entity.OrganizationId && x.CampId == entity.CampId).ToArrayAsync(cancellationToken);
        var ids = items.Select(x => x.Id).ToArray();
        var responsibilities = await dbContext.ShoppingItemResponsibilities.AsNoTracking().Where(x => ids.Contains(x.ShoppingItemId)).ToArrayAsync(cancellationToken);
        return new ShoppingListRecord(entity.Id, entity.OrganizationId, entity.CampId, entity.Name, items.Select(x => ToRecord(x, responsibilities.Where(r => r.ShoppingItemId == x.Id).Select(r => r.UserId).ToArray())).ToArray(), entity.Version, entity.ChangeSequence, entity.DeletedAt, entity.PurgeAt);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new LogisticsRuleException("version_conflict", "Der Datensatz wurde zwischenzeitlich geändert.", exception); }
    }

    private static MaterialRequirementRecord ToRecord(MaterialRequirementEntity x, IReadOnlyList<Guid> users) => new(x.Id, x.OrganizationId, x.CampId, x.Name, x.Description, new LogisticsQuantity(x.QuantityValue, x.QuantityUnit, x.CustomUnitName), users, x.ProcurementSource, x.Note, x.Status, x.ScheduleEntryId, x.Version, x.DeletedAt, x.PurgeAt);
    private static MaterialRequirementEntity ToEntity(MaterialRequirementRecord x) => new() { Id = x.Id, OrganizationId = x.OrganizationId, CampId = x.CampId, Name = x.Name, Description = x.Description, QuantityValue = x.Quantity.Value, QuantityUnit = x.Quantity.Unit, CustomUnitName = x.Quantity.CustomUnitName, ProcurementSource = x.ProcurementSource, Note = x.Note, Status = x.Status, ScheduleEntryId = x.ScheduleEntryId, Version = x.Version, DeletedAt = x.DeletedAt, PurgeAt = x.PurgeAt };
    private static IEnumerable<MaterialResponsibilityEntity> MaterialResponsibilities(MaterialRequirementRecord x) => x.ResponsibleUserIds.Select(userId => new MaterialResponsibilityEntity { MaterialRequirementId = x.Id, UserId = userId, OrganizationId = x.OrganizationId, CampId = x.CampId });
    private static ShoppingListEntity ToEntity(ShoppingListRecord x) => new() { Id = x.Id, OrganizationId = x.OrganizationId, CampId = x.CampId, Name = x.Name, Version = x.Version, ChangeSequence = x.ChangeSequence, DeletedAt = x.DeletedAt, PurgeAt = x.PurgeAt };
    private static ShoppingItemRecord ToRecord(ShoppingItemEntity x, IReadOnlyList<Guid> users) => new(x.Id, x.OrganizationId, x.CampId, x.ShoppingListId, new ShoppingItemContent(x.Name, new LogisticsQuantity(x.QuantityValue, x.QuantityUnit, x.CustomUnitName), users, x.Store, x.Note), ToSource(x), x.IsChecked, x.CheckedByUserId, x.CheckedAt, x.Version);
    private static ShoppingItemEntity ToEntity(ShoppingItemRecord x) => new() { Id = x.Id, OrganizationId = x.OrganizationId, CampId = x.CampId, ShoppingListId = x.ShoppingListId, Name = x.Name, QuantityValue = x.Quantity.Value, QuantityUnit = x.Quantity.Unit, CustomUnitName = x.Quantity.CustomUnitName, Store = x.Store, Note = x.Note, SourceKind = x.Source.Kind, SourceLabel = x.Source.Label, CateringMealId = x.Source.Catering?.MealId, CateringRecipeSnapshotId = x.Source.Catering?.RecipeSnapshotId, CateringSnapshotIngredientId = x.Source.Catering?.SnapshotIngredientId, CateringSourceRecipeId = x.Source.Catering?.SourceRecipeId, CateringSourceRecipeVersionNumber = x.Source.Catering?.SourceRecipeVersionNumber, MaterialRequirementId = x.Source.Material?.MaterialRequirementId, MaterialRequirementVersion = x.Source.Material?.RequirementVersion, IsChecked = x.IsChecked, CheckedByUserId = x.CheckedByUserId, CheckedAt = x.CheckedAt, Version = x.Version };
    private static ShoppingItemSource ToSource(ShoppingItemEntity x) => new(x.SourceKind, x.SourceLabel, x.SourceKind == ShoppingSourceKind.Catering ? new CateringSourceReference(x.CateringMealId!.Value, x.CateringRecipeSnapshotId!.Value, x.CateringSnapshotIngredientId!.Value, x.CateringSourceRecipeId!.Value, x.CateringSourceRecipeVersionNumber!.Value) : null, x.SourceKind == ShoppingSourceKind.MaterialRequirement ? new MaterialSourceReference(x.MaterialRequirementId!.Value, x.MaterialRequirementVersion!.Value) : null);
    private static IEnumerable<ShoppingItemResponsibilityEntity> ItemResponsibilities(ShoppingItemRecord x) => x.ResponsibleUserIds.Select(userId => new ShoppingItemResponsibilityEntity { ShoppingListId = x.ShoppingListId, ShoppingItemId = x.Id, UserId = userId, OrganizationId = x.OrganizationId, CampId = x.CampId });
    private static ShoppingCheckEventEntity ToEntity(ShoppingCheckEventRecord x) => new() { Id = x.Id, OrganizationId = x.OrganizationId, CampId = x.CampId, ShoppingListId = x.ShoppingListId, ShoppingItemId = x.ShoppingItemId, Action = x.Action, ActorId = x.ActorId, OccurredAt = x.OccurredAt, ResultingItemVersion = x.ResultingItemVersion };
    private static ShoppingCheckEventRecord ToRecord(ShoppingCheckEventEntity x) => new(x.Id, x.OrganizationId, x.CampId, x.ShoppingListId, x.ShoppingItemId, x.Action, x.ActorId, x.OccurredAt, x.ResultingItemVersion);
    private static LogisticsRuleException Rule(string code, string message) => new(code, message);
}

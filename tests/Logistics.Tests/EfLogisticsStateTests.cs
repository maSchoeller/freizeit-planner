using Logistics.Contracts;
using Logistics.Implementation;
using Identity.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Logistics.Tests;

public sealed class EfLogisticsStateTests
{
    [Fact]
    public async Task RelationalAdapterPersistsMaterialShoppingAndAuditLifecycles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new LogisticsDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var service = new LogisticsPlanningService(
            new EfLogisticsState(database),
            new TestAccessControl(),
            new TestCampDefaults(),
            new TestScheduleReferences(),
            new FixedTimeProvider(new DateTimeOffset(2027, 8, 2, 10, 15, 0, TimeSpan.Zero)));

        var material = await service.CreateAsync(
            new CreateMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                "Fußbälle",
                "Für das Turnier",
                new LogisticsQuantity(4, LogisticsUnit.Piece),
                [LogisticsFixture.ActorId],
                "Sporthandel",
                "Luftpumpe mitnehmen",
                ProcurementStatus.Open,
                LogisticsFixture.ScheduleEntryId),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListAsync(
            new MaterialQuery(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId, LogisticsFixture.CampId),
            cancellationToken));
        Assert.Equal(material.Id, (await service.GetAsync(
            new MaterialRequest(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, material.Id), cancellationToken))?.Id);

        var updatedMaterial = await service.UpdateAsync(
            new UpdateMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                "Fünf Fußbälle",
                material.Description,
                new LogisticsQuantity(5, LogisticsUnit.Piece),
                [LogisticsFixture.ActorId],
                material.ProcurementSource,
                material.Note,
                ProcurementStatus.Planned,
                material.ScheduleEntryId,
                material.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        await service.DeleteAsync(
            new DeleteMaterialRequirement(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, material.Id, updatedMaterial.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListTrashAsync(
            new MaterialTrashQuery(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId), cancellationToken));
        var restoredMaterial = await service.RestoreAsync(
            new RestoreMaterialRequirement(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, material.Id, updatedMaterial.Version + 1),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(4, restoredMaterial.Version);

        var list = await service.CreateListAsync(
            new CreateShoppingList(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, "Wocheneinkauf"),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListAsync(
            new ShoppingListsQuery(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId), cancellationToken));
        var renamed = await service.RenameListAsync(
            new RenameShoppingList(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id, "Großeinkauf", list.Version),
            cancellationToken);
        database.ChangeTracker.Clear();

        var added = await service.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                new ShoppingItemContent("Äpfel", new LogisticsQuantity(3, LogisticsUnit.Kilogram),
                    [LogisticsFixture.ActorId], "Markt", "regional"),
                renamed.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        var item = Assert.IsType<ShoppingItem>(added.Item);
        var edited = await service.UpdateItemAsync(
            new UpdateShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                item.Id,
                new ShoppingItemContent("Bio-Äpfel", item.Quantity, item.ResponsibleUserIds,
                    item.Store, item.Note),
                item.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        var editedItem = Assert.IsType<ShoppingItem>(edited.Item);
        var checkedChange = await service.SetItemCheckedAsync(
            new SetShoppingItemChecked(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id, item.Id, true, editedItem.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        var checkedItem = Assert.IsType<ShoppingItem>(checkedChange.Item);
        Assert.Single(await service.ListCheckEventsAsync(
            new ShoppingCheckAuditQuery(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id, item.Id), cancellationToken));

        var deletedItem = await service.DeleteItemAsync(
            new DeleteShoppingItem(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id, item.Id, checkedItem.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListItemTrashAsync(
            new ShoppingItemTrashQuery(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId), cancellationToken));
        var restoredItem = await service.RestoreItemAsync(
            new RestoreShoppingItem(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id, item.Id, checkedItem.Version + 1),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.NotNull(restoredItem.Item);
        Assert.True(deletedItem.ChangeSequence < restoredItem.ChangeSequence);

        var currentList = Assert.IsType<ShoppingList>(await service.GetAsync(
            new ShoppingListRequest(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id), cancellationToken));
        await service.DeleteListAsync(
            new DeleteShoppingList(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id, currentList.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListTrashAsync(
            new ShoppingTrashQuery(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId), cancellationToken));
        var restoredList = await service.RestoreListAsync(
            new RestoreShoppingList(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id, currentList.Version + 1),
            cancellationToken);
        Assert.Equal(currentList.Version + 2, restoredList.Version);

        database.ChangeTracker.Clear();
        var erasure = new LogisticsDataErasure(database);
        var pseudonymized = await erasure.PseudonymizeUserAsync(
            LogisticsFixture.ActorId, Guid.Empty, 50, cancellationToken);
        Assert.True(pseudonymized.ChangedRecords >= 4);
        Assert.False(pseudonymized.HasRemaining);
        var erased = await erasure.EraseOrganizationAsync(
            LogisticsFixture.OrganizationId, 50, cancellationToken);
        Assert.True(erased.ChangedRecords >= 2);
        Assert.False(erased.HasRemaining);
        Assert.Equal("logistics", erasure.Area);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => erasure.EraseOrganizationAsync(
            LogisticsFixture.OrganizationId, 0, cancellationToken));
    }
}

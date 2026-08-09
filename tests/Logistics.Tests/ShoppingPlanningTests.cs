using Logistics.Contracts;
using Xunit;

namespace Logistics.Tests;

public sealed class ShoppingPlanningTests
{
    [Fact]
    public async Task NamedListUsesOneItemShapeForSpontaneousAndEditedItems()
    {
        var fixture = LogisticsFixture.Create();
        var list = await fixture.AddListAsync();
        var added = await fixture.Subject.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                Content("Müllbeutel", 3m, LogisticsUnit.Custom, "Rollen"),
                list.Version),
            TestContext.Current.CancellationToken);
        var item = Assert.IsType<ShoppingItem>(added.Item);
        var edited = await fixture.Subject.UpdateItemAsync(
            new UpdateShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                item.Id,
                Content("Große Müllbeutel", 4m, LogisticsUnit.Custom, "Rollen"),
                item.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(ShoppingSourceKind.Spontaneous, edited.Item!.Source.Kind);
        Assert.Equal("Große Müllbeutel", edited.Item.Name);
        Assert.Equal(2, edited.ListVersion);
        Assert.Equal(3, edited.ChangeSequence);
    }

    [Fact]
    public async Task MaterialTransferKeepsImmutableVersionedProvenanceAfterEditing()
    {
        var fixture = LogisticsFixture.Create();
        var material = await fixture.AddMaterialAsync();
        var list = await fixture.AddListAsync();
        var transferred = await fixture.Subject.TransferMaterialAsync(
            new TransferMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                list.Version,
                material.Id,
                material.Version,
                Content("Turnierbälle", 6m, LogisticsUnit.Piece)),
            TestContext.Current.CancellationToken);
        var item = Assert.Single(transferred.Items);
        var edited = await fixture.Subject.UpdateItemAsync(
            new UpdateShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                item.Id,
                Content("Turnierbälle Größe 5", 8m, LogisticsUnit.Piece),
                item.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(material.Id, edited.Item!.Source.Material!.MaterialRequirementId);
        Assert.Equal(material.Version, edited.Item.Source.Material.RequirementVersion);
        Assert.Equal("Turnierbälle Größe 5", edited.Item.Name);
    }

    [Fact]
    public async Task CateringTransferKeepsExactSnapshotLineSourceAndIsAtomic()
    {
        var fixture = LogisticsFixture.Create();
        var list = await fixture.AddListAsync();
        var source = new CateringSourceReference(
            Guid.Parse("41000000-0000-0000-0000-000000000001"),
            Guid.Parse("42000000-0000-0000-0000-000000000001"),
            Guid.Parse("43000000-0000-0000-0000-000000000001"),
            Guid.Parse("44000000-0000-0000-0000-000000000001"),
            3);
        var transferred = await fixture.Subject.TransferCateringAsync(
            new TransferCateringShoppingItems(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                list.Version,
                [new CateringShoppingLine(source, "Abendessen – Gemüsereis", Content("Reis", 2.5m, LogisticsUnit.Kilogram))]),
            TestContext.Current.CancellationToken);
        var item = Assert.Single(transferred.Items);

        var invalid = await Assert.ThrowsAsync<LogisticsRuleException>(() =>
            fixture.Subject.TransferCateringAsync(
                new TransferCateringShoppingItems(
                    LogisticsFixture.ActorId,
                    LogisticsFixture.OrganizationId,
                    LogisticsFixture.CampId,
                    list.Id,
                    transferred.ListVersion,
                    [
                        new CateringShoppingLine(source, "Quelle", Content("Milch", 2m, LogisticsUnit.Liter)),
                        new CateringShoppingLine(source with { SnapshotIngredientId = Guid.Empty }, "Fehler", Content("Salz", 1m, LogisticsUnit.Gram))
                    ]),
                TestContext.Current.CancellationToken));

        Assert.Equal(source, item.Source.Catering);
        Assert.Equal("invalid_source", invalid.ErrorCode);
        Assert.Single(fixture.State.Lists.Single().Items);
    }

    [Fact]
    public async Task CheckAndReopenAreCasProtectedAndAppendImmutableAudit()
    {
        var fixture = LogisticsFixture.Create();
        var list = await fixture.AddListAsync();
        var added = await fixture.Subject.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                Content("Äpfel", 12m, LogisticsUnit.Piece),
                list.Version),
            TestContext.Current.CancellationToken);
        var item = added.Item!;
        var checkedItem = await fixture.Subject.SetItemCheckedAsync(
            Check(list.Id, item.Id, true, item.Version),
            TestContext.Current.CancellationToken);
        var stale = await Assert.ThrowsAsync<LogisticsRuleException>(() =>
            fixture.Subject.SetItemCheckedAsync(
                Check(list.Id, item.Id, true, item.Version),
                TestContext.Current.CancellationToken));
        var reopened = await fixture.Subject.SetItemCheckedAsync(
            Check(list.Id, item.Id, false, checkedItem.Item!.Version),
            TestContext.Current.CancellationToken);
        var audit = await fixture.Subject.ListCheckEventsAsync(
            new ShoppingCheckAuditQuery(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                item.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal("version_conflict", stale.ErrorCode);
        Assert.False(reopened.Item!.IsChecked);
        Assert.Null(reopened.Item.CheckedByUserId);
        Assert.Equal([ShoppingCheckAction.Checked, ShoppingCheckAction.Reopened], audit.Select(x => x.Action));
        Assert.All(audit, x => Assert.Equal(new DateTimeOffset(2027, 8, 2, 10, 15, 0, TimeSpan.Zero), x.OccurredAt));
    }

    [Fact]
    public async Task ChecksOnDifferentItemsDoNotShareAListCas()
    {
        var fixture = LogisticsFixture.Create();
        var list = await fixture.AddListAsync();
        var first = await fixture.Subject.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId, LogisticsFixture.CampId, list.Id, Content("Brot", 2m, LogisticsUnit.Piece), list.Version),
            TestContext.Current.CancellationToken);
        var second = await fixture.Subject.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId, LogisticsFixture.CampId, list.Id, Content("Käse", 1m, LogisticsUnit.Kilogram), first.ListVersion),
            TestContext.Current.CancellationToken);

        var checkedFirst = await fixture.Subject.SetItemCheckedAsync(Check(list.Id, first.Item!.Id, true, 1), TestContext.Current.CancellationToken);
        var checkedSecond = await fixture.Subject.SetItemCheckedAsync(Check(list.Id, second.Item!.Id, true, 1), TestContext.Current.CancellationToken);

        Assert.True(checkedFirst.Item!.IsChecked);
        Assert.True(checkedSecond.Item!.IsChecked);
        Assert.Equal(5, checkedSecond.ChangeSequence);
    }

    [Fact]
    public async Task CampManagerCanRestoreADeletedShoppingListWithItsItems()
    {
        var fixture = LogisticsFixture.Create();
        var list = await fixture.AddListAsync();
        _ = await fixture.Subject.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                Content("Brot", 2m, LogisticsUnit.Piece),
                list.Version),
            TestContext.Current.CancellationToken);
        await fixture.Subject.DeleteListAsync(
            new DeleteShoppingList(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                list.Version + 1),
            TestContext.Current.CancellationToken);

        var trash = await fixture.Subject.ListTrashAsync(
            new ShoppingTrashQuery(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId),
            TestContext.Current.CancellationToken);
        var deleted = Assert.Single(trash);
        var restored = await fixture.Subject.RestoreListAsync(
            new RestoreShoppingList(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                deleted.Version),
            TestContext.Current.CancellationToken);

        Assert.Single(restored.Items);
        Assert.Equal(list.Version + 3, restored.Version);
    }

    [Fact]
    public async Task RetentionPurgesShoppingListItemsAndCheckAuditAtThirtyDays()
    {
        var fixture = LogisticsFixture.Create();
        var list = await fixture.AddListAsync();
        var added = await fixture.Subject.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                Content("Brot", 2m, LogisticsUnit.Piece),
                list.Version),
            TestContext.Current.CancellationToken);
        _ = await fixture.Subject.SetItemCheckedAsync(
            Check(list.Id, added.Item!.Id, true, added.Item.Version),
            TestContext.Current.CancellationToken);
        await fixture.Subject.DeleteListAsync(
            new DeleteShoppingList(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                added.ListVersion),
            TestContext.Current.CancellationToken);
        fixture.Clock.Advance(TimeSpan.FromDays(30));

        var result = await new Logistics.Implementation.LogisticsRetentionService(
            fixture.State,
            fixture.Clock).PurgeExpiredAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.PurgedShoppingLists);
        Assert.Empty(fixture.State.Lists);
        Assert.Empty(fixture.State.Audit);
    }

    [Fact]
    public async Task CampManagerCanRestoreAnIndividuallyDeletedShoppingItem()
    {
        var fixture = LogisticsFixture.Create();
        var list = await fixture.AddListAsync();
        var added = await fixture.Subject.AddSpontaneousItemAsync(
            new AddSpontaneousShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                Content("Brot", 2m, LogisticsUnit.Piece),
                list.Version),
            TestContext.Current.CancellationToken);
        await fixture.Subject.DeleteItemAsync(
            new DeleteShoppingItem(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                list.Id,
                added.Item!.Id,
                added.Item.Version),
            TestContext.Current.CancellationToken);

        var hidden = await fixture.Subject.GetAsync(
            new ShoppingListRequest(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id),
            TestContext.Current.CancellationToken);
        var trash = await fixture.Subject.ListItemTrashAsync(
            new ShoppingItemTrashQuery(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId),
            TestContext.Current.CancellationToken);
        var deleted = Assert.Single(trash);
        var restored = await fixture.Subject.RestoreItemAsync(
            new RestoreShoppingItem(LogisticsFixture.ActorId, LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId, list.Id, deleted.Id, deleted.Version),
            TestContext.Current.CancellationToken);

        Assert.Empty(hidden!.Items);
        Assert.Equal("Brot", restored.Item!.Name);
        Assert.Equal(added.Item.Version + 2, restored.Item.Version);
    }

    private static ShoppingItemContent Content(
        string name,
        decimal value,
        LogisticsUnit unit,
        string? customUnitName = null) => new(
        name,
        new LogisticsQuantity(value, unit, customUnitName),
        [],
        null,
        null);

    private static SetShoppingItemChecked Check(Guid listId, Guid itemId, bool isChecked, long version) => new(
        LogisticsFixture.ActorId,
        LogisticsFixture.OrganizationId,
        LogisticsFixture.CampId,
        listId,
        itemId,
        isChecked,
        version);
}

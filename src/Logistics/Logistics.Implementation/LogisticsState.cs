namespace Logistics.Implementation;

public interface ILogisticsState
{
    ValueTask<IReadOnlyList<MaterialRequirementRecord>> ListMaterialsAsync(Guid organizationId, Guid campId, CancellationToken cancellationToken);
    ValueTask<MaterialRequirementRecord?> FindMaterialAsync(Guid organizationId, Guid campId, Guid materialId, CancellationToken cancellationToken);
    ValueTask AddMaterialAsync(MaterialRequirementRecord material, CancellationToken cancellationToken);
    ValueTask SaveMaterialAsync(MaterialRequirementRecord material, long expectedVersion, CancellationToken cancellationToken);
    ValueTask DeleteMaterialAsync(MaterialRequirementRecord material, long expectedVersion, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ShoppingListRecord>> ListShoppingListsAsync(Guid organizationId, Guid campId, CancellationToken cancellationToken);
    ValueTask<ShoppingListRecord?> FindShoppingListAsync(Guid organizationId, Guid campId, Guid shoppingListId, CancellationToken cancellationToken);
    ValueTask AddShoppingListAsync(ShoppingListRecord list, CancellationToken cancellationToken);
    ValueTask SaveShoppingListAsync(ShoppingListRecord list, long expectedVersion, CancellationToken cancellationToken);
    ValueTask DeleteShoppingListAsync(ShoppingListRecord list, long expectedVersion, CancellationToken cancellationToken);
    ValueTask AddShoppingItemsAsync(ShoppingListRecord list, IReadOnlyList<ShoppingItemRecord> items, long expectedListVersion, CancellationToken cancellationToken);
    ValueTask SaveShoppingItemAsync(ShoppingListRecord list, ShoppingItemRecord item, long expectedItemVersion, ShoppingCheckEventRecord? auditEvent, CancellationToken cancellationToken);
    ValueTask DeleteShoppingItemAsync(ShoppingListRecord list, ShoppingItemRecord item, long expectedItemVersion, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<ShoppingCheckEventRecord>> ListCheckEventsAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId, CancellationToken cancellationToken);
}

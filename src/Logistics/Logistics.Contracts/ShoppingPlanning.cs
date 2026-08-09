namespace Logistics.Contracts;

public interface IShoppingPlanning
{
    Task<IReadOnlyList<ShoppingListSummary>> ListAsync(
        ShoppingListsQuery query,
        CancellationToken cancellationToken);

    Task<ShoppingList?> GetAsync(ShoppingListRequest request, CancellationToken cancellationToken);

    Task<ShoppingList> CreateListAsync(
        CreateShoppingList command,
        CancellationToken cancellationToken);

    Task<ShoppingList> RenameListAsync(
        RenameShoppingList command,
        CancellationToken cancellationToken);

    Task DeleteListAsync(DeleteShoppingList command, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrashedShoppingList>> ListTrashAsync(
        ShoppingTrashQuery query,
        CancellationToken cancellationToken);

    Task<ShoppingList> RestoreListAsync(
        RestoreShoppingList command,
        CancellationToken cancellationToken);

    Task<ShoppingListChange> AddSpontaneousItemAsync(
        AddSpontaneousShoppingItem command,
        CancellationToken cancellationToken);

    Task<ShoppingListChange> UpdateItemAsync(
        UpdateShoppingItem command,
        CancellationToken cancellationToken);

    Task<ShoppingListChange> SetItemCheckedAsync(
        SetShoppingItemChecked command,
        CancellationToken cancellationToken);

    Task<ShoppingListChange> DeleteItemAsync(
        DeleteShoppingItem command,
        CancellationToken cancellationToken);
}

public interface IShoppingTransfer
{
    Task<ShoppingTransferResult> TransferCateringAsync(
        TransferCateringShoppingItems command,
        CancellationToken cancellationToken);

    Task<ShoppingTransferResult> TransferMaterialAsync(
        TransferMaterialRequirement command,
        CancellationToken cancellationToken);
}

public interface IShoppingAudit
{
    Task<IReadOnlyList<ShoppingCheckEvent>> ListCheckEventsAsync(
        ShoppingCheckAuditQuery query,
        CancellationToken cancellationToken);
}

public sealed record ShoppingListsQuery(Guid ActorId, Guid OrganizationId, Guid CampId);

public sealed record ShoppingListRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId);

public sealed record CreateShoppingList(Guid ActorId, Guid OrganizationId, Guid CampId, string Name);

public sealed record RenameShoppingList(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    string Name,
    long ExpectedListVersion);

public sealed record DeleteShoppingList(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    long ExpectedListVersion);

public sealed record ShoppingTrashQuery(Guid ActorId, Guid OrganizationId, Guid CampId);

public sealed record RestoreShoppingList(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    long ExpectedListVersion);

public sealed record TrashedShoppingList(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    DateTimeOffset DeletedAt,
    DateTimeOffset PurgeAt,
    long Version);

public sealed record ShoppingItemContent(
    string Name,
    LogisticsQuantity Quantity,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string? Store,
    string? Note);

public sealed record AddSpontaneousShoppingItem(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    ShoppingItemContent Content,
    long ExpectedListVersion);

public sealed record UpdateShoppingItem(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    Guid ShoppingItemId,
    ShoppingItemContent Content,
    long ExpectedItemVersion);

public sealed record SetShoppingItemChecked(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    Guid ShoppingItemId,
    bool IsChecked,
    long ExpectedItemVersion);

public sealed record DeleteShoppingItem(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    Guid ShoppingItemId,
    long ExpectedItemVersion);

public sealed record ShoppingListSummary(
    Guid Id,
    string Name,
    int OpenItemCount,
    int CheckedItemCount,
    long Version,
    long ChangeSequence);

public sealed record ShoppingList(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    IReadOnlyList<ShoppingItem> Items,
    long Version,
    long ChangeSequence);

public sealed record ShoppingItem(
    Guid Id,
    Guid ShoppingListId,
    string Name,
    LogisticsQuantity Quantity,
    IReadOnlyList<Guid> ResponsibleUserIds,
    string? Store,
    string? Note,
    ShoppingItemSource Source,
    bool IsChecked,
    Guid? CheckedByUserId,
    DateTimeOffset? CheckedAt,
    long Version);

public sealed record ShoppingListChange(
    Guid ShoppingListId,
    long ListVersion,
    long ChangeSequence,
    ShoppingItem? Item);

public sealed record ShoppingItemSource(
    ShoppingSourceKind Kind,
    string Label,
    CateringSourceReference? Catering = null,
    MaterialSourceReference? Material = null);

public sealed record CateringSourceReference(
    Guid MealId,
    Guid RecipeSnapshotId,
    Guid SnapshotIngredientId,
    Guid SourceRecipeId,
    int SourceRecipeVersionNumber);

public sealed record MaterialSourceReference(Guid MaterialRequirementId, long RequirementVersion);

public sealed record CateringShoppingLine(
    CateringSourceReference Source,
    string SourceLabel,
    ShoppingItemContent Content);

public sealed record TransferCateringShoppingItems(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    long ExpectedListVersion,
    IReadOnlyList<CateringShoppingLine> Lines);

public sealed record TransferMaterialRequirement(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    long ExpectedListVersion,
    Guid MaterialRequirementId,
    long ExpectedRequirementVersion,
    ShoppingItemContent Content);

public sealed record ShoppingTransferResult(
    Guid ShoppingListId,
    long ListVersion,
    long ChangeSequence,
    IReadOnlyList<ShoppingItem> Items);

public sealed record ShoppingCheckAuditQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    Guid ShoppingItemId);

public sealed record ShoppingCheckEvent(
    Guid Id,
    Guid ShoppingItemId,
    ShoppingCheckAction Action,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    long ResultingItemVersion);

public enum ShoppingSourceKind
{
    Spontaneous,
    Catering,
    MaterialRequirement
}

public enum ShoppingCheckAction
{
    Checked,
    Reopened
}

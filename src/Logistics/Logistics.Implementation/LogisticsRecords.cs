using Logistics.Contracts;

namespace Logistics.Implementation;

public sealed class MaterialRequirementRecord
{
    public MaterialRequirementRecord(
        Guid id,
        Guid organizationId,
        Guid campId,
        string name,
        string? description,
        LogisticsQuantity quantity,
        IReadOnlyList<Guid> responsibleUserIds,
        string? procurementSource,
        string? note,
        ProcurementStatus status,
        Guid? scheduleEntryId,
        long version = 1,
        DateTimeOffset? deletedAt = null,
        DateTimeOffset? purgeAt = null)
    {
        Id = id;
        OrganizationId = organizationId;
        CampId = campId;
        Apply(name, description, quantity, responsibleUserIds, procurementSource, note, status, scheduleEntryId);
        Version = version;
        DeletedAt = deletedAt;
        PurgeAt = purgeAt;
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid CampId { get; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public LogisticsQuantity Quantity { get; private set; } = null!;
    public IReadOnlyList<Guid> ResponsibleUserIds { get; private set; } = [];
    public string? ProcurementSource { get; private set; }
    public string? Note { get; private set; }
    public ProcurementStatus Status { get; private set; }
    public Guid? ScheduleEntryId { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? PurgeAt { get; private set; }

    public void Update(
        string name,
        string? description,
        LogisticsQuantity quantity,
        IReadOnlyList<Guid> responsibleUserIds,
        string? procurementSource,
        string? note,
        ProcurementStatus status,
        Guid? scheduleEntryId,
        long expectedVersion)
    {
        RequireVersion(expectedVersion);
        Apply(name, description, quantity, responsibleUserIds, procurementSource, note, status, scheduleEntryId);
        Version++;
    }

    public void RequireVersion(long expectedVersion)
    {
        if (Version != expectedVersion) throw Rule("version_conflict", "Der Materialbedarf wurde zwischenzeitlich geändert.");
    }

    public void MoveToTrash(long expectedVersion, DateTimeOffset now)
    {
        RequireVersion(expectedVersion);
        if (DeletedAt is not null)
        {
            throw Rule("material_already_trashed", "Der Materialbedarf befindet sich bereits im Papierkorb.");
        }
        DeletedAt = now;
        PurgeAt = now.AddDays(30);
        Version++;
    }

    public void Restore(long expectedVersion, DateTimeOffset now)
    {
        RequireVersion(expectedVersion);
        if (DeletedAt is null || PurgeAt is null)
        {
            throw Rule("material_not_trashed", "Der Materialbedarf befindet sich nicht im Papierkorb.");
        }
        if (PurgeAt <= now)
        {
            throw Rule("material_restore_expired", "Die Aufbewahrungsfrist ist abgelaufen.");
        }
        DeletedAt = null;
        PurgeAt = null;
        Version++;
    }

    private void Apply(
        string name,
        string? description,
        LogisticsQuantity quantity,
        IReadOnlyList<Guid> responsibleUserIds,
        string? procurementSource,
        string? note,
        ProcurementStatus status,
        Guid? scheduleEntryId)
    {
        Name = name;
        Description = description;
        Quantity = quantity;
        ResponsibleUserIds = responsibleUserIds.ToArray();
        ProcurementSource = procurementSource;
        Note = note;
        Status = status;
        ScheduleEntryId = scheduleEntryId;
    }

    private static LogisticsRuleException Rule(string code, string message) => new(code, message);
}

public sealed class ShoppingListRecord
{
    public ShoppingListRecord(
        Guid id,
        Guid organizationId,
        Guid campId,
        string name,
        IReadOnlyList<ShoppingItemRecord>? items = null,
        long version = 1,
        long changeSequence = 1,
        DateTimeOffset? deletedAt = null,
        DateTimeOffset? purgeAt = null)
    {
        Id = id;
        OrganizationId = organizationId;
        CampId = campId;
        Name = name;
        Items = items?.ToList() ?? [];
        Version = version;
        ChangeSequence = changeSequence;
        DeletedAt = deletedAt;
        PurgeAt = purgeAt;
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid CampId { get; }
    public string Name { get; private set; }
    public List<ShoppingItemRecord> Items { get; }
    public long Version { get; private set; }
    public long ChangeSequence { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? PurgeAt { get; private set; }

    public void Rename(string name, long expectedVersion)
    {
        RequireVersion(expectedVersion);
        Name = name;
        Version++;
        ChangeSequence++;
    }

    public void AddItems(IReadOnlyList<ShoppingItemRecord> items, long expectedVersion)
    {
        RequireVersion(expectedVersion);
        Items.AddRange(items);
        Version++;
        ChangeSequence++;
    }

    public void AdvanceItemChange() => ChangeSequence++;

    public void RemoveItem(ShoppingItemRecord item)
    {
        Items.Remove(item);
        Version++;
        ChangeSequence++;
    }

    public void AdvanceItemLifecycle()
    {
        Version++;
        ChangeSequence++;
    }

    public void SynchronizeRevision(long version, long changeSequence)
    {
        Version = version;
        ChangeSequence = changeSequence;
    }

    public void RequireVersion(long expectedVersion)
    {
        if (Version != expectedVersion) throw Rule("version_conflict", "Die Einkaufsliste wurde zwischenzeitlich geändert.");
    }

    public void MoveToTrash(long expectedVersion, DateTimeOffset now)
    {
        RequireVersion(expectedVersion);
        if (DeletedAt is not null) throw Rule("shopping_list_already_trashed", "Die Einkaufsliste befindet sich bereits im Papierkorb.");
        DeletedAt = now;
        PurgeAt = now.AddDays(30);
        Version++;
        ChangeSequence++;
    }

    public void Restore(long expectedVersion, DateTimeOffset now)
    {
        RequireVersion(expectedVersion);
        if (DeletedAt is null || PurgeAt is null) throw Rule("shopping_list_not_trashed", "Die Einkaufsliste befindet sich nicht im Papierkorb.");
        if (PurgeAt <= now) throw Rule("shopping_list_restore_expired", "Die Aufbewahrungsfrist ist abgelaufen.");
        DeletedAt = null;
        PurgeAt = null;
        Version++;
        ChangeSequence++;
    }

    private static LogisticsRuleException Rule(string code, string message) => new(code, message);
}

public sealed class ShoppingItemRecord
{
    public ShoppingItemRecord(
        Guid id,
        Guid organizationId,
        Guid campId,
        Guid shoppingListId,
        ShoppingItemContent content,
        ShoppingItemSource source,
        bool isChecked = false,
        Guid? checkedByUserId = null,
        DateTimeOffset? checkedAt = null,
        long version = 1,
        DateTimeOffset? deletedAt = null,
        DateTimeOffset? purgeAt = null)
    {
        Id = id;
        OrganizationId = organizationId;
        CampId = campId;
        ShoppingListId = shoppingListId;
        Source = source;
        Apply(content);
        IsChecked = isChecked;
        CheckedByUserId = checkedByUserId;
        CheckedAt = checkedAt;
        Version = version;
        DeletedAt = deletedAt;
        PurgeAt = purgeAt;
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid CampId { get; }
    public Guid ShoppingListId { get; }
    public string Name { get; private set; } = string.Empty;
    public LogisticsQuantity Quantity { get; private set; } = null!;
    public IReadOnlyList<Guid> ResponsibleUserIds { get; private set; } = [];
    public string? Store { get; private set; }
    public string? Note { get; private set; }
    public ShoppingItemSource Source { get; }
    public bool IsChecked { get; private set; }
    public Guid? CheckedByUserId { get; private set; }
    public DateTimeOffset? CheckedAt { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? PurgeAt { get; private set; }

    public void Update(ShoppingItemContent content, long expectedVersion)
    {
        RequireVersion(expectedVersion);
        Apply(content);
        Version++;
    }

    public ShoppingCheckEventRecord SetChecked(
        bool isChecked,
        Guid actorId,
        DateTimeOffset occurredAt,
        long expectedVersion)
    {
        RequireVersion(expectedVersion);
        IsChecked = isChecked;
        CheckedByUserId = isChecked ? actorId : null;
        CheckedAt = isChecked ? occurredAt : null;
        Version++;
        return new ShoppingCheckEventRecord(
            Guid.NewGuid(),
            OrganizationId,
            CampId,
            ShoppingListId,
            Id,
            isChecked ? ShoppingCheckAction.Checked : ShoppingCheckAction.Reopened,
            actorId,
            occurredAt,
            Version);
    }

    public void RequireVersion(long expectedVersion)
    {
        if (Version != expectedVersion) throw Rule("version_conflict", "Die Einkaufsposition wurde zwischenzeitlich geändert.");
    }

    public void MoveToTrash(long expectedVersion, DateTimeOffset now)
    {
        RequireVersion(expectedVersion);
        if (DeletedAt is not null) throw Rule("shopping_item_already_trashed", "Die Einkaufsposition befindet sich bereits im Papierkorb.");
        DeletedAt = now;
        PurgeAt = now.AddDays(30);
        Version++;
    }

    public void Restore(long expectedVersion, DateTimeOffset now)
    {
        RequireVersion(expectedVersion);
        if (DeletedAt is null || PurgeAt is null) throw Rule("shopping_item_not_trashed", "Die Einkaufsposition befindet sich nicht im Papierkorb.");
        if (PurgeAt <= now) throw Rule("shopping_item_restore_expired", "Die Aufbewahrungsfrist ist abgelaufen.");
        DeletedAt = null;
        PurgeAt = null;
        Version++;
    }

    private void Apply(ShoppingItemContent content)
    {
        Name = content.Name;
        Quantity = content.Quantity;
        ResponsibleUserIds = content.ResponsibleUserIds.ToArray();
        Store = content.Store;
        Note = content.Note;
    }

    private static LogisticsRuleException Rule(string code, string message) => new(code, message);
}

public sealed record ShoppingCheckEventRecord(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    Guid ShoppingListId,
    Guid ShoppingItemId,
    ShoppingCheckAction Action,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    long ResultingItemVersion);

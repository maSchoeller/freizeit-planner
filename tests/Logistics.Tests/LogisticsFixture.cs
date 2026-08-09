using Camps.Contracts;
using Identity.Contracts;
using Logistics.Contracts;
using Logistics.Implementation;
using Xunit;

namespace Logistics.Tests;

internal sealed class LogisticsFixture
{
    public static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid CampId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid ScheduleEntryId = Guid.Parse("31000000-0000-0000-0000-000000000001");

    private LogisticsFixture(
        TestLogisticsState state,
        TestAccessControl access,
        TestCampDefaults camp,
        TestScheduleReferences schedule)
    {
        State = state;
        Access = access;
        Camp = camp;
        Schedule = schedule;
        Clock = new FixedTimeProvider(new DateTimeOffset(2027, 8, 2, 10, 15, 0, TimeSpan.Zero));
        Subject = new LogisticsPlanningService(
            state,
            access,
            camp,
            schedule,
            Clock);
    }

    public TestLogisticsState State { get; }
    public TestAccessControl Access { get; }
    public TestCampDefaults Camp { get; }
    public TestScheduleReferences Schedule { get; }
    public FixedTimeProvider Clock { get; }
    public LogisticsPlanningService Subject { get; }

    public static LogisticsFixture Create() => new(
        new TestLogisticsState(),
        new TestAccessControl(),
        new TestCampDefaults(),
        new TestScheduleReferences());

    public Task<MaterialRequirement> AddMaterialAsync(Guid? scheduleEntryId = null) =>
        Subject.CreateAsync(
            new CreateMaterialRequirement(
                ActorId,
                OrganizationId,
                CampId,
                "Fußbälle",
                null,
                new LogisticsQuantity(4m, LogisticsUnit.Piece),
                [ActorId],
                null,
                null,
                ProcurementStatus.Open,
                scheduleEntryId),
            TestContext.Current.CancellationToken);

    public Task<ShoppingList> AddListAsync(string name = "Wocheneinkauf") =>
        Subject.CreateListAsync(
            new CreateShoppingList(ActorId, OrganizationId, CampId, name),
            TestContext.Current.CancellationToken);
}

internal sealed class TestLogisticsState : ILogisticsState
{
    public List<MaterialRequirementRecord> Materials { get; } = [];
    public List<ShoppingListRecord> Lists { get; } = [];
    public List<ShoppingCheckEventRecord> Audit { get; } = [];

    public ValueTask<IReadOnlyList<MaterialRequirementRecord>> ListMaterialsAsync(Guid organizationId, Guid campId, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<MaterialRequirementRecord>>(Materials.Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.DeletedAt is null).ToArray());

    public ValueTask<MaterialRequirementRecord?> FindMaterialAsync(Guid organizationId, Guid campId, Guid materialId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Materials.SingleOrDefault(x => x.OrganizationId == organizationId && x.CampId == campId && x.Id == materialId && x.DeletedAt is null));

    public ValueTask AddMaterialAsync(MaterialRequirementRecord material, CancellationToken cancellationToken)
    {
        Materials.Add(material);
        return ValueTask.CompletedTask;
    }

    public ValueTask SaveMaterialAsync(MaterialRequirementRecord material, long expectedVersion, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<MaterialRequirementRecord>> ListDeletedMaterialsAsync(Guid organizationId, Guid campId, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<MaterialRequirementRecord>>(Materials.Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.DeletedAt is not null).ToArray());

    public ValueTask<MaterialRequirementRecord?> FindDeletedMaterialAsync(Guid organizationId, Guid campId, Guid materialId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Materials.SingleOrDefault(x => x.OrganizationId == organizationId && x.CampId == campId && x.Id == materialId && x.DeletedAt is not null));

    public ValueTask<int> PurgeDueMaterialsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var due = Materials.Where(x => x.PurgeAt <= now).Take(batchSize).ToArray();
        var removed = Materials.RemoveAll(due.Contains);
        return ValueTask.FromResult(removed);
    }

    public ValueTask<IReadOnlyList<ShoppingListRecord>> ListShoppingListsAsync(Guid organizationId, Guid campId, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ShoppingListRecord>>(Lists.Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.DeletedAt is null).ToArray());

    public ValueTask<ShoppingListRecord?> FindShoppingListAsync(Guid organizationId, Guid campId, Guid shoppingListId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Lists.SingleOrDefault(x => x.OrganizationId == organizationId && x.CampId == campId && x.Id == shoppingListId && x.DeletedAt is null));

    public ValueTask AddShoppingListAsync(ShoppingListRecord list, CancellationToken cancellationToken)
    {
        Lists.Add(list);
        return ValueTask.CompletedTask;
    }

    public ValueTask SaveShoppingListAsync(ShoppingListRecord list, long expectedVersion, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<ShoppingListRecord>> ListDeletedShoppingListsAsync(Guid organizationId, Guid campId, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ShoppingListRecord>>(Lists.Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.DeletedAt is not null).ToArray());

    public ValueTask<ShoppingListRecord?> FindDeletedShoppingListAsync(Guid organizationId, Guid campId, Guid shoppingListId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Lists.SingleOrDefault(x => x.OrganizationId == organizationId && x.CampId == campId && x.Id == shoppingListId && x.DeletedAt is not null));

    public ValueTask<int> PurgeDueShoppingListsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var due = Lists.Where(x => x.PurgeAt <= now).Take(batchSize).ToArray();
        var dueIds = due.Select(x => x.Id).ToHashSet();
        Audit.RemoveAll(x => dueIds.Contains(x.ShoppingListId));
        var removed = Lists.RemoveAll(due.Contains);
        return ValueTask.FromResult(removed);
    }

    public ValueTask AddShoppingItemsAsync(ShoppingListRecord list, IReadOnlyList<ShoppingItemRecord> items, long expectedListVersion, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask SaveShoppingItemAsync(ShoppingListRecord list, ShoppingItemRecord item, long expectedItemVersion, ShoppingCheckEventRecord? auditEvent, CancellationToken cancellationToken)
    {
        if (auditEvent is not null) Audit.Add(auditEvent);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteShoppingItemAsync(ShoppingListRecord list, ShoppingItemRecord item, long expectedItemVersion, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<ShoppingCheckEventRecord>> ListCheckEventsAsync(Guid organizationId, Guid campId, Guid listId, Guid itemId, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ShoppingCheckEventRecord>>(Audit.Where(x => x.OrganizationId == organizationId && x.CampId == campId && x.ShoppingListId == listId && x.ShoppingItemId == itemId).ToArray());
}

internal sealed class TestAccessControl : ITenantAccessControl
{
    public bool DenyAll { get; set; }
    public HashSet<Guid> DeniedActors { get; } = [];
    public HashSet<CampAction> DeniedCampActions { get; } = [];

    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(OrganizationAccessRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Decision(request.ActorId));

    public Task<TenantAccessDecision> AuthorizeCampAsync(CampAccessRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(DeniedCampActions.Contains(request.Action)
            ? TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied)
            : Decision(request.ActorId));

    private TenantAccessDecision Decision(Guid actorId) => DenyAll || DeniedActors.Contains(actorId)
        ? TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied)
        : TenantAccessDecision.Permit(TenantRole.Member);
}

internal sealed class TestCampDefaults : ICampPlanningDefaults
{
    public CampStatus Status { get; set; } = CampStatus.Active;

    public Task<CampPlanningDefaults> GetAsync(CampAccessQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new CampPlanningDefaults(query.CampId, 20, Status, 1));
}

internal sealed class TestScheduleReferences : IScheduleReferenceAccess
{
    public bool Deny { get; set; }

    public Task<ScheduleEntryReference> RequireAsync(ScheduleEntryReferenceRequest request, CancellationToken cancellationToken)
    {
        if (Deny) throw new CampsRuleException("schedule_entry_not_found", "Der Zeitplaneintrag wurde nicht gefunden.");
        return Task.FromResult(new ScheduleEntryReference(request.OrganizationId, request.CampId, request.ScheduleEntryId, 1));
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan duration) => now = now.Add(duration);
}

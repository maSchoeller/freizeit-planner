using Camps.Contracts;
using Camps.Implementation;
using Identity.Contracts;
using Xunit;
using static Camps.Tests.CampFixture;

namespace Camps.Tests;

public sealed class CampPlanningTests
{
    [Fact]
    public async Task OwnerCanCreateCampWithValidatedPlanningDefaults()
    {
        var camps = CampFixture.Create().Management;

        var camp = await camps.CreateAsync(
            new CreateCamp(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                "Sommerfreizeit",
                "sommerfreizeit",
                "Gemeinsame Woche am See",
                new DateOnly(2027, 7, 31),
                new DateOnly(2027, 8, 7),
                null,
                42),
            TestContext.Current.CancellationToken);

        Assert.Equal("Europe/Berlin", camp.TimeZoneId);
        Assert.Equal(42, camp.DefaultPortions);
        Assert.Equal(CampStatus.Active, camp.Status);
        Assert.Equal(1, camp.Version);
    }

    [Fact]
    public async Task ArchivedCampIsReadableAndCanBeReactivatedButCannotBeEdited()
    {
        var fixture = CampFixture.Create();
        var archived = await fixture.AddCampAsync(CampStatus.Archived);
        var cancellationToken = TestContext.Current.CancellationToken;

        var read = await fixture.Management.GetBySlugAsync(
            new CampBySlugQuery(ActorId, OrganizationId, archived.Slug),
            cancellationToken);
        var edit = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Management.UpdateAsync(
                new UpdateCamp(
                    ActorId,
                    OrganizationId,
                    archived.Id,
                    archived.Name,
                    archived.Slug,
                    archived.Description,
                    archived.StartsOn,
                    archived.EndsOn,
                    archived.TimeZoneId,
                    archived.DefaultPortions,
                    archived.Version),
                cancellationToken));
        var reactivated = await fixture.Management.ChangeStatusAsync(
            new ChangeCampStatus(
                ActorId,
                OrganizationId,
                archived.Id,
                CampStatus.Active,
                archived.Version),
            cancellationToken);

        Assert.Equal(CampStatus.Archived, read.Status);
        Assert.Equal("camp_archived", edit.ErrorCode);
        Assert.Equal(CampStatus.Active, reactivated.Status);
        Assert.Equal(archived.Version + 1, reactivated.Version);
    }

    [Fact]
    public async Task CampRejectsDuplicateSlugInvalidDefaultsAndStaleVersion()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var duplicate = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Management.CreateAsync(
                new CreateCamp(
                    ActorId,
                    OrganizationId,
                    "Noch ein Camp",
                    camp.Slug,
                    null,
                    camp.StartsOn,
                    camp.EndsOn,
                    camp.TimeZoneId,
                    10),
                cancellationToken));
        var portions = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Management.UpdateAsync(
                new UpdateCamp(
                    ActorId,
                    OrganizationId,
                    camp.Id,
                    camp.Name,
                    camp.Slug,
                    null,
                    camp.StartsOn,
                    camp.EndsOn,
                    camp.TimeZoneId,
                    0,
                    camp.Version),
                cancellationToken));
        var stale = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Management.ChangeStatusAsync(
                new ChangeCampStatus(
                    ActorId,
                    OrganizationId,
                    camp.Id,
                    CampStatus.Archived,
                    camp.Version + 1),
                cancellationToken));

        Assert.Equal("camp_slug_conflict", duplicate.ErrorCode);
        Assert.Equal("invalid_default_portions", portions.ErrorCode);
        Assert.Equal("version_conflict", stale.ErrorCode);
    }

    [Fact]
    public async Task PlanningDefaultsExposePositivePortionsAndCurrentVersion()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync(defaultPortions: 73);

        var defaults = await fixture.Management.GetAsync(
            new CampAccessQuery(ActorId, OrganizationId, camp.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(new CampPlanningDefaults(camp.Id, 73, CampStatus.Active, 1), defaults);
    }

    [Fact]
    public async Task TenantAuthorizationDenialCannotBeBypassedWithIdentifiers()
    {
        var access = new PermitAllTenantAccess { DenyAll = true };
        var fixture = CampFixture.Create(access);

        var denied = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Management.CreateAsync(
                new CreateCamp(
                    ActorId,
                    OrganizationId,
                    "Fremdes Camp",
                    "fremdes-camp",
                    null,
                    new DateOnly(2027, 8, 1),
                    new DateOnly(2027, 8, 2),
                    "Europe/Berlin",
                    10),
                TestContext.Current.CancellationToken));

        Assert.Equal("camp_access_denied", denied.ErrorCode);
        Assert.Empty(fixture.State.Camps);
    }

    [Fact]
    public async Task CampListDistinguishesUpcomingOngoingAndPastPeriods()
    {
        var fixture = CampFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.Management.CreateAsync(
            Camp("Vergangen", "vergangen", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 7)),
            cancellationToken);
        await fixture.Management.CreateAsync(
            Camp("Laufend", "laufend", new DateOnly(2027, 7, 31), new DateOnly(2027, 8, 7)),
            cancellationToken);
        await fixture.Management.CreateAsync(
            Camp("Zukünftig", "zukuenftig", new DateOnly(2027, 9, 1), new DateOnly(2027, 9, 7)),
            cancellationToken);

        var camps = await fixture.Management.ListAsync(
            new CampListQuery(ActorId, OrganizationId),
            cancellationToken);

        Assert.Equal(CampPeriod.Past, camps.Single(item => item.Slug == "vergangen").Period);
        Assert.Equal(CampPeriod.Ongoing, camps.Single(item => item.Slug == "laufend").Period);
        Assert.Equal(CampPeriod.Upcoming, camps.Single(item => item.Slug == "zukuenftig").Period);
    }

    [Fact]
    public async Task CampUpdateChangesPlanningDefaultsAndIncrementsVersion()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();

        var updated = await fixture.Management.UpdateAsync(
            new UpdateCamp(
                ActorId,
                OrganizationId,
                camp.Id,
                "Sommerfreizeit 2027",
                "sommerfreizeit-2027",
                "Aktualisiert",
                camp.StartsOn,
                camp.EndsOn,
                "Europe/Berlin",
                55,
                camp.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal("sommerfreizeit-2027", updated.Slug);
        Assert.Equal(55, updated.DefaultPortions);
        Assert.Equal(camp.Version + 1, updated.Version);
    }

    private static CreateCamp Camp(
        string name,
        string slug,
        DateOnly startsOn,
        DateOnly endsOn) => new(
        ActorId,
        OrganizationId,
        name,
        slug,
        null,
        startsOn,
        endsOn,
        "Europe/Berlin",
        25);
}

internal sealed class CampFixture
{
    public static readonly Guid ActorId = Guid.Parse("71000000-0000-0000-0000-000000000001");

    public static readonly Guid OrganizationId = Guid.Parse("72000000-0000-0000-0000-000000000001");

    private CampFixture(
        CampsTestState state,
        CampPlanningService management,
        PermitAllTenantAccess access)
    {
        State = state;
        Management = management;
        Schedule = new SchedulePlanningService(state, management, access);
    }

    public CampsTestState State { get; }

    public CampPlanningService Management { get; }

    public SchedulePlanningService Schedule { get; }

    public static CampFixture Create(PermitAllTenantAccess? access = null)
    {
        var state = new CampsTestState();
        access ??= new PermitAllTenantAccess();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2027, 8, 2, 10, 0, 0, TimeSpan.Zero));
        return new CampFixture(state, new CampPlanningService(state, access, clock), access);
    }

    public async Task<CampView> AddCampAsync(
        CampStatus status = CampStatus.Active,
        int defaultPortions = 42)
    {
        var camp = await Management.CreateAsync(
            new CreateCamp(
                ActorId,
                OrganizationId,
                "Sommerfreizeit",
                "sommerfreizeit",
                null,
                new DateOnly(2027, 7, 31),
                new DateOnly(2027, 8, 7),
                "Europe/Berlin",
                defaultPortions),
            TestContext.Current.CancellationToken);
        return status == CampStatus.Active
            ? camp
            : await Management.ChangeStatusAsync(
                new ChangeCampStatus(
                    ActorId,
                    OrganizationId,
                    camp.Id,
                    status,
                    camp.Version),
                TestContext.Current.CancellationToken);
    }
}

internal sealed class CampsTestState : ICampsState
{
    public List<CampRecord> Camps { get; } = [];

    public List<ScheduleEntryRecord> ScheduleEntries { get; } = [];

    public ValueTask<IReadOnlyList<CampRecord>> ListCampsAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<CampRecord>>(
            Camps.Where(item => item.OrganizationId == organizationId).ToArray());

    public ValueTask<CampRecord?> FindCampAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Camps.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.Id == campId));

    public ValueTask<CampRecord?> FindCampBySlugAsync(
        Guid organizationId,
        string slug,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Camps.SingleOrDefault(item =>
            item.OrganizationId == organizationId && item.Slug == slug));

    public ValueTask AddCampAsync(CampRecord camp, CancellationToken cancellationToken)
    {
        Camps.Add(camp);
        return ValueTask.CompletedTask;
    }

    public ValueTask SaveCampAsync(
        CampRecord camp,
        long expectedVersion,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<ScheduleEntryRecord>> ListScheduleEntriesAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ScheduleEntryRecord>>(
            ScheduleEntries.Where(item =>
                item.OrganizationId == organizationId && item.CampId == campId).ToArray());

    public ValueTask<ScheduleEntryRecord?> FindScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ScheduleEntries.SingleOrDefault(item =>
            item.OrganizationId == organizationId
            && item.CampId == campId
            && item.Id == scheduleEntryId));

    public ValueTask AddScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        CancellationToken cancellationToken)
    {
        ScheduleEntries.Add(scheduleEntry);
        return ValueTask.CompletedTask;
    }

    public ValueTask SaveScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        long expectedVersion,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DeleteScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ScheduleEntries.Remove(scheduleEntry);
        return ValueTask.CompletedTask;
    }
}

internal sealed class PermitAllTenantAccess : ITenantAccessControl
{
    public bool DenyAll { get; init; }

    public HashSet<Guid> DeniedActorIds { get; } = [];

    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(DenyAll || DeniedActorIds.Contains(request.ActorId)
            ? TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied)
            : TenantAccessDecision.Permit(TenantRole.Owner));

    public Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(DenyAll || DeniedActorIds.Contains(request.ActorId)
            ? TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied)
            : TenantAccessDecision.Permit(TenantRole.Owner));
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

using Camps.Contracts;
using Logistics.Contracts;
using Logistics.Implementation;
using Xunit;

namespace Logistics.Tests;

public sealed class MaterialPlanningTests
{
    [Fact]
    public async Task MemberCanCreateCampWideMaterialRequirement()
    {
        var fixture = LogisticsFixture.Create();

        var material = await fixture.Subject.CreateAsync(
            new CreateMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                "Fußbälle",
                "Für das Turnier",
                new LogisticsQuantity(4m, LogisticsUnit.Piece),
                [LogisticsFixture.ActorId],
                "Sportgeschäft",
                null,
                ProcurementStatus.Open,
                null),
            TestContext.Current.CancellationToken);

        Assert.Equal(4m, material.Quantity.Value);
        Assert.Null(material.ScheduleEntryId);
        Assert.Equal(1, material.Version);
    }

    [Fact]
    public async Task ScheduleLinkedMaterialRequiresValidReferenceAndArchivedCampIsReadOnly()
    {
        var fixture = LogisticsFixture.Create();
        var linked = await fixture.AddMaterialAsync(LogisticsFixture.ScheduleEntryId);
        fixture.Schedule.Deny = true;

        var invalidLink = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.AddMaterialAsync(Guid.NewGuid()));
        fixture.Schedule.Deny = false;
        fixture.Camp.Status = CampStatus.Archived;
        var archived = await Assert.ThrowsAsync<LogisticsRuleException>(() =>
            fixture.Subject.UpdateAsync(
                new UpdateMaterialRequirement(
                    LogisticsFixture.ActorId,
                    LogisticsFixture.OrganizationId,
                    LogisticsFixture.CampId,
                    linked.Id,
                    linked.Name,
                    linked.Description,
                    linked.Quantity,
                    linked.ResponsibleUserIds,
                    linked.ProcurementSource,
                    linked.Note,
                    ProcurementStatus.Procured,
                    linked.ScheduleEntryId,
                    linked.Version),
                TestContext.Current.CancellationToken));

        Assert.Equal("schedule_entry_not_found", invalidLink.ErrorCode);
        Assert.Equal("camp_archived", archived.ErrorCode);
        Assert.Single(fixture.State.Materials);
    }

    [Fact]
    public async Task MaterialUpdateAndDeleteRequireLatestVersion()
    {
        var fixture = LogisticsFixture.Create();
        var material = await fixture.AddMaterialAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var updated = await fixture.Subject.UpdateAsync(
            new UpdateMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                "Turnierbälle",
                null,
                new LogisticsQuantity(6m, LogisticsUnit.Piece),
                [LogisticsFixture.ActorId],
                "Sportgeschäft",
                null,
                ProcurementStatus.Planned,
                null,
                material.Version),
            cancellationToken);

        var stale = await Assert.ThrowsAsync<LogisticsRuleException>(() =>
            fixture.Subject.DeleteAsync(
                new DeleteMaterialRequirement(
                    LogisticsFixture.ActorId,
                    LogisticsFixture.OrganizationId,
                    LogisticsFixture.CampId,
                    material.Id,
                    material.Version),
                cancellationToken));
        await fixture.Subject.DeleteAsync(
            new DeleteMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                updated.Version),
            cancellationToken);

        Assert.Equal("version_conflict", stale.ErrorCode);
        Assert.Empty(await fixture.Subject.ListAsync(
            new MaterialQuery(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId),
            cancellationToken));
    }

    [Fact]
    public async Task CampManagerCanRestoreSoftDeletedMaterialBeforeThePurgeDeadline()
    {
        var fixture = LogisticsFixture.Create();
        var material = await fixture.AddMaterialAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.Subject.DeleteAsync(
            new DeleteMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                material.Version),
            cancellationToken);

        var trash = await fixture.Subject.ListTrashAsync(
            new MaterialTrashQuery(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId),
            cancellationToken);
        var deleted = Assert.Single(trash);
        var restored = await fixture.Subject.RestoreAsync(
            new RestoreMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                deleted.Version),
            cancellationToken);

        Assert.Equal(new DateTimeOffset(2027, 9, 1, 10, 15, 0, TimeSpan.Zero), deleted.PurgeAt);
        Assert.Equal(material.Version + 2, restored.Version);
        Assert.Single(fixture.State.Materials);
    }

    [Fact]
    public async Task RetentionPermanentlyRemovesMaterialAtThirtyDays()
    {
        var fixture = LogisticsFixture.Create();
        var material = await fixture.AddMaterialAsync();
        await fixture.Subject.DeleteAsync(
            new DeleteMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                material.Version),
            TestContext.Current.CancellationToken);
        var retention = new LogisticsRetentionService(fixture.State, fixture.Clock);

        var before = await retention.PurgeExpiredAsync(10, TestContext.Current.CancellationToken);
        fixture.Clock.Advance(TimeSpan.FromDays(30));
        var due = await retention.PurgeExpiredAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(0, before.PurgedMaterials);
        Assert.Equal(1, due.PurgedMaterials);
        Assert.Empty(fixture.State.Materials);
    }

    [Fact]
    public async Task MemberCannotBrowseOrRestoreMaterialTrash()
    {
        var fixture = LogisticsFixture.Create();
        var material = await fixture.AddMaterialAsync();
        await fixture.Subject.DeleteAsync(
            new DeleteMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                material.Version),
            TestContext.Current.CancellationToken);
        fixture.Access.DeniedCampActions.Add(Identity.Contracts.CampAction.ManageCamp);

        var browse = await Assert.ThrowsAsync<LogisticsRuleException>(() => fixture.Subject.ListTrashAsync(
            new MaterialTrashQuery(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId),
            TestContext.Current.CancellationToken));
        var restore = await Assert.ThrowsAsync<LogisticsRuleException>(() => fixture.Subject.RestoreAsync(
            new RestoreMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                material.Version + 1),
            TestContext.Current.CancellationToken));

        Assert.Equal("access_denied", browse.ErrorCode);
        Assert.Equal("access_denied", restore.ErrorCode);
    }

    [Fact]
    public async Task ExpiredOrArchivedMaterialCannotBeRestored()
    {
        var fixture = LogisticsFixture.Create();
        var material = await fixture.AddMaterialAsync();
        await fixture.Subject.DeleteAsync(
            new DeleteMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                material.Version),
            TestContext.Current.CancellationToken);
        fixture.Clock.Advance(TimeSpan.FromDays(30));
        var expired = await Assert.ThrowsAsync<LogisticsRuleException>(() => fixture.Subject.RestoreAsync(
            new RestoreMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                material.Version + 1),
            TestContext.Current.CancellationToken));
        fixture.Clock.Advance(TimeSpan.FromDays(-30));
        fixture.Camp.Status = CampStatus.Archived;
        var archived = await Assert.ThrowsAsync<LogisticsRuleException>(() => fixture.Subject.RestoreAsync(
            new RestoreMaterialRequirement(
                LogisticsFixture.ActorId,
                LogisticsFixture.OrganizationId,
                LogisticsFixture.CampId,
                material.Id,
                material.Version + 1),
            TestContext.Current.CancellationToken));

        Assert.Equal("material_restore_expired", expired.ErrorCode);
        Assert.Equal("camp_archived", archived.ErrorCode);
    }

    [Fact]
    public async Task ResponsibilityMustHaveCampAccessAndDeniedActorCannotReachState()
    {
        var fixture = LogisticsFixture.Create();
        var foreignUser = Guid.Parse("90000000-0000-0000-0000-000000000001");
        fixture.Access.DeniedActors.Add(foreignUser);

        var responsibility = await Assert.ThrowsAsync<LogisticsRuleException>(() =>
            fixture.Subject.CreateAsync(
                new CreateMaterialRequirement(
                    LogisticsFixture.ActorId,
                    LogisticsFixture.OrganizationId,
                    LogisticsFixture.CampId,
                    "Seile",
                    null,
                    new LogisticsQuantity(2m, LogisticsUnit.Custom, "Rollen"),
                    [foreignUser],
                    null,
                    null,
                    ProcurementStatus.Open,
                    null),
                TestContext.Current.CancellationToken));
        fixture.Access.DenyAll = true;
        var actor = await Assert.ThrowsAsync<LogisticsRuleException>(() => fixture.AddMaterialAsync());

        Assert.Equal("invalid_responsibility", responsibility.ErrorCode);
        Assert.Equal("access_denied", actor.ErrorCode);
        Assert.Empty(fixture.State.Materials);
    }

    [Fact]
    public void QuantitiesConvertOnlyWithinCompatibleDimensions()
    {
        var kilograms = new LogisticsQuantity(1500m, LogisticsUnit.Gram)
            .ConvertTo(LogisticsUnit.Kilogram);
        var liters = new LogisticsQuantity(2500m, LogisticsUnit.Milliliter)
            .ConvertTo(LogisticsUnit.Liter);
        var custom = new LogisticsQuantity(2m, LogisticsUnit.Custom, "  Kabel\tTrommeln ")
            .ConvertTo(LogisticsUnit.Custom, "Kabel Trommeln");

        var incompatible = Assert.Throws<LogisticsRuleException>(() =>
            new LogisticsQuantity(1m, LogisticsUnit.Kilogram).ConvertTo(LogisticsUnit.Liter));
        var wrongCustom = Assert.Throws<LogisticsRuleException>(() =>
            custom.ConvertTo(LogisticsUnit.Custom, "Rollen"));

        Assert.Equal(1.5m, kilograms.Value);
        Assert.Equal(2.5m, liters.Value);
        Assert.Equal("Kabel Trommeln", custom.CustomUnitName);
        Assert.Equal("incompatible_unit", incompatible.ErrorCode);
        Assert.Equal("incompatible_unit", wrongCustom.ErrorCode);
    }
}

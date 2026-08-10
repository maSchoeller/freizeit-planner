using Camps.Contracts;
using Camps.Implementation;
using Identity.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Camps.Tests;

public sealed class EfCampsStateTests
{
    [Fact]
    public async Task RelationalAdapterPersistsTheCompleteCampAndScheduleLifecycle()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<CampsDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new CampsDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var state = new EfCampsState(database);
        var access = new PermitAllTenantAccess();
        var now = new DateTimeOffset(2027, 8, 2, 10, 0, 0, TimeSpan.Zero);
        var management = new CampPlanningService(state, access, new FixedTimeProvider(now));
        var schedule = new SchedulePlanningService(
            state,
            management,
            access,
            new FixedTimeProvider(now));

        var camp = await management.CreateAsync(
            new CreateCamp(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                "Sommerfreizeit",
                "sommerfreizeit",
                "Am See",
                new DateOnly(2027, 7, 31),
                new DateOnly(2027, 8, 7),
                "Europe/Berlin",
                42),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await management.ListAsync(
            new CampListQuery(CampFixture.ActorId, CampFixture.OrganizationId),
            cancellationToken));
        Assert.Equal(camp.Id, (await management.GetBySlugAsync(
            new CampBySlugQuery(CampFixture.ActorId, CampFixture.OrganizationId, camp.Slug),
            cancellationToken)).Id);

        var updatedCamp = await management.UpdateAsync(
            new UpdateCamp(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                "Sommerfreizeit 2027",
                "sommerfreizeit-2027",
                camp.Description,
                camp.StartsOn,
                camp.EndsOn,
                camp.TimeZoneId,
                45,
                camp.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(2, updatedCamp.Version);

        var entry = await schedule.CreateAsync(
            new CreateScheduleEntry(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                new ScheduleTimingInput(
                    true,
                    null,
                    null,
                    new DateOnly(2027, 8, 2),
                    new DateOnly(2027, 8, 3)),
                "Geländespiel",
                "Im Wald",
                "Wald",
                "Programm",
                ScheduleEntryStatus.Planned,
                [CampFixture.ActorId],
                "Alle"),
            cancellationToken);
        database.ChangeTracker.Clear();
        var loaded = await schedule.GetAsync(
            new ScheduleEntryQuery(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                entry.Id),
            cancellationToken);
        Assert.Equal(entry.Id, loaded.Id);
        Assert.Single(await schedule.ListAsync(
            new ScheduleRangeQuery(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                new DateOnly(2027, 8, 1),
                new DateOnly(2027, 8, 4)),
            cancellationToken));

        var revised = await schedule.UpdateAsync(
            new UpdateScheduleEntry(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                entry.Id,
                new ScheduleTimingInput(
                    true,
                    null,
                    null,
                    entry.Timing.StartDate,
                    entry.Timing.EndDateExclusive),
                "Großes Geländespiel",
                entry.Description,
                entry.Location,
                entry.Category,
                ScheduleEntryStatus.Confirmed,
                [CampFixture.ActorId],
                entry.Audience,
                entry.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(2, revised.Version);
        Assert.Equal(entry.Id, (await schedule.RequireAsync(
            new ScheduleEntryReferenceRequest(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                entry.Id,
                ScheduleReferencePurpose.LinkForWrite),
            cancellationToken)).ScheduleEntryId);

        var deleted = await schedule.DeleteAsync(
            new DeleteScheduleEntry(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                entry.Id,
                revised.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await schedule.ListTrashAsync(
            new ScheduleTrashQuery(CampFixture.ActorId, CampFixture.OrganizationId, camp.Id),
            cancellationToken));
        var restored = await schedule.RestoreAsync(
            new RestoreScheduleEntry(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                entry.Id,
                deleted.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(4, restored.Version);

        var deletedAgain = await schedule.DeleteAsync(
            new DeleteScheduleEntry(
                CampFixture.ActorId,
                CampFixture.OrganizationId,
                camp.Id,
                entry.Id,
                restored.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(restored.Version + 1, deletedAgain.Version);

        var erasure = new CampsDataErasure(database);
        var pseudonymized = await erasure.PseudonymizeUserAsync(
            CampFixture.ActorId, Guid.Empty, 50, cancellationToken);
        Assert.Equal(1, pseudonymized.ChangedRecords);
        Assert.False(pseudonymized.HasRemaining);

        var erased = await erasure.EraseOrganizationAsync(CampFixture.OrganizationId, 50, cancellationToken);
        Assert.Equal(1, erased.ChangedRecords);
        Assert.False(erased.HasRemaining);
        Assert.Equal("camps", erasure.Area);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            erasure.PseudonymizeUserAsync(CampFixture.ActorId, Guid.Empty, 501, cancellationToken));
    }
}

using Camps.Contracts;
using Xunit;
using static Camps.Tests.CampFixture;

namespace Camps.Tests;

public sealed class SchedulePlanningTests
{
    [Fact]
    public async Task NonexistentLocalTimeIsRejected()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();

        var error = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Schedule.CreateAsync(
                Entry(
                    camp.Id,
                    Timed(new DateTime(2027, 3, 28, 2, 30, 0),
                        new DateTime(2027, 3, 28, 4, 0, 0))),
                TestContext.Current.CancellationToken));

        Assert.Equal("local_time_nonexistent", error.ErrorCode);
    }

    [Fact]
    public async Task AmbiguousLocalTimeRequiresChoiceAndStoresSelectedUtcInstant()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var localStart = new DateTime(2027, 10, 31, 2, 30, 0);
        var localEnd = new DateTime(2027, 10, 31, 3, 30, 0);

        var ambiguous = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Schedule.CreateAsync(
                Entry(camp.Id, Timed(localStart, localEnd)),
                cancellationToken));
        var earlier = await fixture.Schedule.CreateAsync(
            Entry(
                camp.Id,
                Timed(
                    localStart,
                    localEnd,
                    AmbiguousLocalTimeChoice.EarlierOffset)),
            cancellationToken);
        var later = await fixture.Schedule.CreateAsync(
            Entry(
                camp.Id,
                Timed(
                    localStart,
                    localEnd,
                    AmbiguousLocalTimeChoice.LaterOffset)),
            cancellationToken);

        Assert.Equal("local_time_ambiguous", ambiguous.ErrorCode);
        Assert.Equal(
            new DateTimeOffset(2027, 10, 31, 0, 30, 0, TimeSpan.Zero),
            earlier.Timing.StartsAtUtc);
        Assert.Equal(
            new DateTimeOffset(2027, 10, 31, 1, 30, 0, TimeSpan.Zero),
            later.Timing.StartsAtUtc);
    }

    [Fact]
    public async Task AllDayEntryKeepsLocalDatesWithoutUtcShadowValues()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();

        var entry = await fixture.Schedule.CreateAsync(
            Entry(
                camp.Id,
                new ScheduleTimingInput(
                    true,
                    null,
                    null,
                    new DateOnly(2027, 8, 2),
                    new DateOnly(2027, 8, 3))),
            TestContext.Current.CancellationToken);

        Assert.True(entry.Timing.IsAllDay);
        Assert.Null(entry.Timing.StartsAtUtc);
        Assert.Null(entry.Timing.EndsAtUtc);
        Assert.Equal(new DateOnly(2027, 8, 2), entry.Timing.StartDate);
        Assert.Equal(new DateOnly(2027, 8, 3), entry.Timing.EndDateExclusive);
    }

    [Fact]
    public async Task ParallelEntriesAreAllowedAndMarkedWhileAdjacentEntryIsNot()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await fixture.Schedule.CreateAsync(
            Entry(camp.Id, Timed(At(10), At(12)), "Geländespiel"),
            cancellationToken);
        var second = await fixture.Schedule.CreateAsync(
            Entry(camp.Id, Timed(At(11), At(13)), "Kleingruppen"),
            cancellationToken);
        var adjacent = await fixture.Schedule.CreateAsync(
            Entry(camp.Id, Timed(At(13), At(14)), "Pause"),
            cancellationToken);

        var agenda = await fixture.Schedule.ListAsync(
            new ScheduleRangeQuery(
                ActorId,
                OrganizationId,
                camp.Id,
                new DateOnly(2027, 8, 2),
                new DateOnly(2027, 8, 3)),
            cancellationToken);

        Assert.True(agenda.Single(item => item.Id == first.Id).OverlapsAnotherEntry);
        Assert.True(agenda.Single(item => item.Id == second.Id).OverlapsAnotherEntry);
        Assert.False(agenda.Single(item => item.Id == adjacent.Id).OverlapsAnotherEntry);
    }

    [Fact]
    public async Task ArchivedCampBlocksWriteLinksButKeepsReadReferences()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();
        var entry = await fixture.Schedule.CreateAsync(
            Entry(camp.Id, Timed(At(10), At(11))),
            TestContext.Current.CancellationToken);
        var archived = await fixture.Management.ChangeStatusAsync(
            new ChangeCampStatus(
                ActorId,
                OrganizationId,
                camp.Id,
                CampStatus.Archived,
                camp.Version),
            TestContext.Current.CancellationToken);

        var readable = await fixture.Schedule.RequireAsync(
            new ScheduleEntryReferenceRequest(
                ActorId,
                OrganizationId,
                archived.Id,
                entry.Id,
                ScheduleReferencePurpose.Read),
            TestContext.Current.CancellationToken);
        var blocked = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Schedule.RequireAsync(
                new ScheduleEntryReferenceRequest(
                    ActorId,
                    OrganizationId,
                    archived.Id,
                    entry.Id,
                    ScheduleReferencePurpose.LinkForWrite),
                TestContext.Current.CancellationToken));

        Assert.Equal(entry.Id, readable.ScheduleEntryId);
        Assert.Equal("camp_archived", blocked.ErrorCode);
    }

    [Fact]
    public async Task StaleUpdateIsRejectedAndOriginalEntryRemainsReadable()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();
        var entry = await fixture.Schedule.CreateAsync(
            Entry(camp.Id, Timed(At(10), At(11))),
            TestContext.Current.CancellationToken);

        var stale = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Schedule.UpdateAsync(
                new UpdateScheduleEntry(
                    ActorId,
                    OrganizationId,
                    camp.Id,
                    entry.Id,
                    Timed(At(12), At(13)),
                    "Geändert",
                    null,
                    null,
                    "Programm",
                    ScheduleEntryStatus.Confirmed,
                    [ActorId],
                    null,
                    entry.Version + 1),
                TestContext.Current.CancellationToken));
        var unchanged = await fixture.Schedule.GetAsync(
            new ScheduleEntryQuery(ActorId, OrganizationId, camp.Id, entry.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal("version_conflict", stale.ErrorCode);
        Assert.Equal("Programmpunkt", unchanged.Title);
        Assert.Equal(entry.Version, unchanged.Version);
    }

    [Fact]
    public async Task ResponsibilityMustBelongToTheCamp()
    {
        var foreignUserId = Guid.Parse("73000000-0000-0000-0000-000000000001");
        var access = new PermitAllTenantAccess();
        access.DeniedActorIds.Add(foreignUserId);
        var fixture = CampFixture.Create(access);
        var camp = await fixture.AddCampAsync();

        var denied = await Assert.ThrowsAsync<CampsRuleException>(() =>
            fixture.Schedule.CreateAsync(
                Entry(camp.Id, Timed(At(10), At(11))) with
                {
                    ResponsibleUserIds = [foreignUserId]
                },
                TestContext.Current.CancellationToken));

        Assert.Equal("invalid_responsibility", denied.ErrorCode);
        Assert.Empty(fixture.State.ScheduleEntries);
    }

    [Fact]
    public async Task EntryCanBeUpdatedAndDeletedWithTheLatestVersion()
    {
        var fixture = CampFixture.Create();
        var camp = await fixture.AddCampAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var entry = await fixture.Schedule.CreateAsync(
            Entry(camp.Id, Timed(At(10), At(11))),
            cancellationToken);

        var updated = await fixture.Schedule.UpdateAsync(
            new UpdateScheduleEntry(
                ActorId,
                OrganizationId,
                camp.Id,
                entry.Id,
                Timed(At(12), At(14)),
                "Nachmittagsprogramm",
                "Draußen",
                "Sportplatz",
                "Programm",
                ScheduleEntryStatus.Confirmed,
                [ActorId],
                "Alle",
                entry.Version),
            cancellationToken);
        var deleted = await fixture.Schedule.DeleteAsync(
            new DeleteScheduleEntry(
                ActorId,
                OrganizationId,
                camp.Id,
                entry.Id,
                updated.Version),
            cancellationToken);

        Assert.Equal(entry.Version + 1, updated.Version);
        Assert.Equal("Nachmittagsprogramm", updated.Title);
        Assert.Equal(entry.Id, deleted.ScheduleEntryId);
        Assert.Empty(fixture.State.ScheduleEntries);
    }

    private static CreateScheduleEntry Entry(
        Guid campId,
        ScheduleTimingInput timing,
        string title = "Programmpunkt") => new(
        ActorId,
        OrganizationId,
        campId,
        timing,
        title,
        null,
        "Gemeindehaus",
        "Programm",
        ScheduleEntryStatus.Planned,
        [ActorId],
        null);

    private static ScheduleTimingInput Timed(
        DateTime start,
        DateTime end,
        AmbiguousLocalTimeChoice startChoice = AmbiguousLocalTimeChoice.Reject) => new(
        false,
        DateTime.SpecifyKind(start, DateTimeKind.Unspecified),
        DateTime.SpecifyKind(end, DateTimeKind.Unspecified),
        null,
        null,
        startChoice,
        AmbiguousLocalTimeChoice.Reject);

    private static DateTime At(int hour) => new(2027, 8, 2, hour, 0, 0);
}

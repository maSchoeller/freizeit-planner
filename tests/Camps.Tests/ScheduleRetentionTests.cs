using Camps.Contracts;
using Camps.Implementation;
using Xunit;

namespace Camps.Tests;

public sealed class ScheduleRetentionTests
{
    [Fact]
    public async Task PurgeUsesCurrentTimeAndValidatesBatchBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTimeOffset(2027, 9, 2, 10, 0, 0, TimeSpan.Zero);
        var state = new CampsTestState();
        var subject = new ScheduleRetentionService(state, new FixedTimeProvider(now));

        state.ScheduleEntries.Add(CreateDeletedEntry(now.AddMinutes(-1)));
        state.ScheduleEntries.Add(CreateDeletedEntry(now.AddMinutes(1)));

        var result = await subject.PurgeExpiredAsync(1, cancellationToken);

        Assert.Equal(1, result.PurgedScheduleEntries);
        Assert.Single(state.ScheduleEntries);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            subject.PurgeExpiredAsync(0, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            subject.PurgeExpiredAsync(501, cancellationToken));
    }

    private static ScheduleEntryRecord CreateDeletedEntry(DateTimeOffset purgeAt) => new(
        Guid.NewGuid(),
        CampFixture.OrganizationId,
        Guid.NewGuid(),
        new ScheduleTimingRecord(true, null, null, new DateOnly(2027, 9, 1), new DateOnly(2027, 9, 2)),
        "Programm",
        null,
        null,
        "Programm",
        ScheduleEntryStatus.Planned,
        [],
        null,
        1,
        purgeAt.AddDays(-30),
        purgeAt);
}

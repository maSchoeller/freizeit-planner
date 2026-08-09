using Camps.Contracts;

namespace Camps.Implementation;

public sealed class ScheduleRetentionService(
    ICampsState state,
    TimeProvider timeProvider) : IScheduleRetention
{
    public async Task<ScheduleRetentionResult> PurgeExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
        var purged = await state.PurgeDueScheduleEntriesAsync(
            timeProvider.GetUtcNow(),
            batchSize,
            cancellationToken);
        return new ScheduleRetentionResult(purged);
    }
}

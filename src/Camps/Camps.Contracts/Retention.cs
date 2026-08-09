namespace Camps.Contracts;

public interface IScheduleRetention
{
    Task<ScheduleRetentionResult> PurgeExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record ScheduleRetentionResult(int PurgedScheduleEntries);

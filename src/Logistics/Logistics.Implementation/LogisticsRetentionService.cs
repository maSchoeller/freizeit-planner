using Logistics.Contracts;

namespace Logistics.Implementation;

public sealed class LogisticsRetentionService(
    ILogisticsState state,
    TimeProvider timeProvider) : ILogisticsRetention
{
    public async Task<LogisticsRetentionResult> PurgeExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var materials = await state.PurgeDueMaterialsAsync(
            timeProvider.GetUtcNow(),
            batchSize,
            cancellationToken);
        return new LogisticsRetentionResult(materials);
    }
}

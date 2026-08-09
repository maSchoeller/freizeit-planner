using Spiritual.Contracts;

namespace Spiritual.Implementation;

public sealed class DevotionRetentionService(
    IDevotionState state,
    TimeProvider timeProvider) : IDevotionRetention
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task<DevotionPurgeResult> PurgeExpiredDevotionsAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var purged = await state.PurgeDueAsync(
            timeProvider.GetUtcNow().Subtract(Retention),
            batchSize,
            cancellationToken);
        return new DevotionPurgeResult(purged);
    }
}

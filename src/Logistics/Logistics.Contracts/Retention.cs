namespace Logistics.Contracts;

public interface ILogisticsRetention
{
    Task<LogisticsRetentionResult> PurgeExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record LogisticsRetentionResult(
    int PurgedMaterials,
    int PurgedShoppingLists = 0,
    int PurgedShoppingItems = 0);

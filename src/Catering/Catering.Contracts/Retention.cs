namespace Catering.Contracts;

public interface IMealRetention
{
    Task<MealRetentionResult> PurgeExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record MealRetentionResult(int PurgedMeals);

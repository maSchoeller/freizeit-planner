using Catering.Contracts;

namespace Catering.Implementation;

public sealed class MealRetentionService : IMealRetention
{
    private readonly ICateringState state;
    private readonly TimeProvider timeProvider;

    public MealRetentionService(CateringDbContext dbContext, TimeProvider timeProvider)
        : this(new EfCateringState(dbContext), timeProvider)
    {
    }

    internal MealRetentionService(ICateringState state, TimeProvider timeProvider)
    {
        this.state = state;
        this.timeProvider = timeProvider;
    }

    public async Task<MealRetentionResult> PurgeExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
        var purged = await state.PurgeDueMealsAsync(
            timeProvider.GetUtcNow(),
            batchSize,
            cancellationToken);
        return new MealRetentionResult(purged);
    }
}

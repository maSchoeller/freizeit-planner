using Catering.Implementation;
using Xunit;

namespace Catering.Tests;

public sealed class MealRetentionTests
{
    [Fact]
    public async Task PurgeUsesCurrentTimeAndValidatesBatchBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTimeOffset(2027, 9, 2, 10, 0, 0, TimeSpan.Zero);
        var state = new TestCateringState();
        var subject = new MealRetentionService(state, new FixedMealTimeProvider(now));

        state.Meals.Add(CreateDeletedMeal(now.AddMinutes(-1)));
        state.Meals.Add(CreateDeletedMeal(now.AddMinutes(1)));

        var result = await subject.PurgeExpiredAsync(1, cancellationToken);

        Assert.Equal(1, result.PurgedMeals);
        Assert.Single(state.Meals);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            subject.PurgeExpiredAsync(0, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            subject.PurgeExpiredAsync(501, cancellationToken));
    }

    private static MealEntity CreateDeletedMeal(DateTimeOffset purgeAt) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
        CampId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
        Name = "Mahlzeit",
        DeletedAt = purgeAt.AddDays(-30),
        PurgeAt = purgeAt,
        Version = 1,
    };

    private sealed class FixedMealTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

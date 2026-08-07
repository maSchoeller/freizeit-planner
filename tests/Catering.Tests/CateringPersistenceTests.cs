using Catering.Implementation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catering.Tests;

public sealed class CateringPersistenceTests
{
    [Fact]
    public void EveryTenantRowCarriesItsDirectSecurityScope()
    {
        using var context = new CateringDbContext(
            new DbContextOptionsBuilder<CateringDbContext>()
                .UseNpgsql("Host=localhost;Database=model_only")
                .Options);
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(IngredientEntity))!.FindProperty(nameof(IngredientEntity.OrganizationId)));
        Assert.NotNull(model.FindEntityType(typeof(RecipeEntity))!.FindProperty(nameof(RecipeEntity.OrganizationId)));
        Assert.NotNull(model.FindEntityType(typeof(RecipeVersionEntity))!.FindProperty(nameof(RecipeVersionEntity.OrganizationId)));
        Assert.NotNull(model.FindEntityType(typeof(RecipeIngredientEntity))!.FindProperty(nameof(RecipeIngredientEntity.OrganizationId)));
        AssertCampScope<MealEntity>(model);
        AssertCampScope<RecipeSnapshotEntity>(model);
        AssertCampScope<SnapshotIngredientEntity>(model);
    }

    private static void AssertCampScope<TEntity>(Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        var entity = model.FindEntityType(typeof(TEntity))!;
        Assert.NotNull(entity.FindProperty("OrganizationId"));
        Assert.NotNull(entity.FindProperty("CampId"));
    }
}

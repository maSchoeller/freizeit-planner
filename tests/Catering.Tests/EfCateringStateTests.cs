using Catering.Contracts;
using Catering.Implementation;
using Identity.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catering.Tests;

public sealed class EfCateringStateTests
{
    private static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CampId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task RelationalAdapterPersistsLibraryRecipeAndMealLifecycles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<CateringDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new CateringDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var service = new CateringService(
            database,
            new AllowAccessControl(),
            new TestCampContext(10, false),
            TimeProvider.System);

        var ingredient = await service.CreateIngredientAsync(
            new CreateIngredient(ActorId, OrganizationId, "Kartoffeln"),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.SearchIngredientsAsync(
            new IngredientSearch(ActorId, OrganizationId, "kart", 20),
            cancellationToken));
        var renamed = await service.RenameIngredientAsync(
            new RenameIngredient(ActorId, OrganizationId, ingredient.Id, "Bio-Kartoffeln", ingredient.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(2, renamed.Version);

        var recipeContent = new RecipeContent(
            "Kartoffelgratin",
            "Sättigende Beilage",
            "Kartoffeln schneiden und backen",
            5,
            [new RecipeIngredientInput(ingredient.Id, new Quantity(1, MeasurementUnit.Kilogram))],
            ["vegetarisch"],
            "Heiß servieren",
            null);
        var recipe = await service.CreateRecipeAsync(
            new CreateRecipe(ActorId, OrganizationId, recipeContent),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListRecipesAsync(
            new OrganizationCateringQuery(ActorId, OrganizationId), cancellationToken));
        Assert.Equal(recipe.Id, (await service.GetRecipeAsync(
            new RecipeRequest(ActorId, OrganizationId, recipe.Id), cancellationToken))?.Id);
        database.ChangeTracker.Clear();

        var meal = await service.CreateMealAsync(
            new CreateMeal(ActorId, OrganizationId, CampId, "Mittagessen", null, null, [recipe.Id]),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListMealsAsync(
            new CampCateringQuery(ActorId, OrganizationId, CampId), cancellationToken));
        Assert.Equal(meal.Id, (await service.GetMealAsync(
            new MealRequest(ActorId, OrganizationId, CampId, meal.Id), cancellationToken))?.Id);
        Assert.Single((await service.PrepareShoppingTransferAsync(
            new MealRequest(ActorId, OrganizationId, CampId, meal.Id), cancellationToken)).Lines);

        var revisedMeal = await service.ReviseMealAsync(
            new ReviseMeal(ActorId, OrganizationId, CampId, meal.Id,
                "Festliches Mittagessen", 12, null, meal.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        await service.MoveMealToTrashAsync(
            new DeleteMeal(ActorId, OrganizationId, CampId, meal.Id, revisedMeal.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        var trashed = Assert.Single(await service.ListMealTrashAsync(
            new MealTrashQuery(ActorId, OrganizationId, CampId), cancellationToken));
        var restored = await service.RestoreMealAsync(
            new RestoreMeal(ActorId, OrganizationId, CampId, meal.Id, trashed.Version),
            cancellationToken);
        Assert.Equal(trashed.Version + 1, restored.Version);
        Assert.Single(restored.RecipeSnapshots);

        var erasure = new CateringDataErasure(database);
        var pseudonymized = await erasure.PseudonymizeUserAsync(ActorId, Guid.Empty, 50, cancellationToken);
        Assert.Equal(0, pseudonymized.ChangedRecords);
        var erased = await erasure.EraseOrganizationAsync(OrganizationId, 50, cancellationToken);
        Assert.Equal(3, erased.ChangedRecords);
        Assert.False(erased.HasRemaining);
        Assert.Equal("catering", erasure.Area);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            erasure.EraseOrganizationAsync(OrganizationId, 0, cancellationToken));
    }
}

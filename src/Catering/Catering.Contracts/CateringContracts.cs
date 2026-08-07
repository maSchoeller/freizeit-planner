using System.Diagnostics;
using System.Text;

namespace Catering.Contracts;

public interface IOrganizationCateringLibrary
{
    Task<IReadOnlyList<Ingredient>> SearchIngredientsAsync(
        IngredientSearch request,
        CancellationToken cancellationToken);

    Task<Ingredient> CreateIngredientAsync(CreateIngredient request, CancellationToken cancellationToken);

    Task<Ingredient> RenameIngredientAsync(RenameIngredient request, CancellationToken cancellationToken);

    Task<IngredientMergePreview> PreviewIngredientMergeAsync(
        IngredientMergeRequest request,
        CancellationToken cancellationToken);

    Task<IngredientMergeResult> MergeIngredientsAsync(
        MergeIngredients request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RecipeSummary>> ListRecipesAsync(
        OrganizationCateringQuery request,
        CancellationToken cancellationToken);

    Task<Recipe?> GetRecipeAsync(RecipeRequest request, CancellationToken cancellationToken);

    Task<Recipe> CreateRecipeAsync(CreateRecipe request, CancellationToken cancellationToken);

    Task<Recipe> ReviseRecipeAsync(ReviseRecipe request, CancellationToken cancellationToken);
}

public interface ICampMealPlanning
{
    Task<IReadOnlyList<MealSummary>> ListMealsAsync(
        CampCateringQuery request,
        CancellationToken cancellationToken);

    Task<Meal?> GetMealAsync(MealRequest request, CancellationToken cancellationToken);

    Task<Meal> CreateMealAsync(CreateMeal request, CancellationToken cancellationToken);

    Task<Meal> ReviseMealAsync(ReviseMeal request, CancellationToken cancellationToken);

    Task<Meal> AddRecipeSnapshotAsync(AddRecipeSnapshot request, CancellationToken cancellationToken);

    Task<Meal> RemoveRecipeSnapshotAsync(RemoveRecipeSnapshot request, CancellationToken cancellationToken);

    Task<Meal> RefreshRecipeSnapshotAsync(RefreshRecipeSnapshot request, CancellationToken cancellationToken);
}

public interface IMealShoppingSource
{
    Task<MealShoppingDraft> PrepareShoppingTransferAsync(
        MealRequest request,
        CancellationToken cancellationToken);
}

public interface ICampCateringContext
{
    Task<CampCateringContext> GetAsync(
        CampCateringContextRequest request,
        CancellationToken cancellationToken);
}

public sealed record OrganizationCateringQuery(Guid ActorId, Guid OrganizationId);

public sealed record CampCateringQuery(Guid ActorId, Guid OrganizationId, Guid CampId);

public sealed record CampCateringContextRequest(Guid OrganizationId, Guid CampId);

public sealed record CampCateringContext(int DefaultPortions, bool IsArchived);

public sealed record IngredientSearch(Guid ActorId, Guid OrganizationId, string Query, int Limit = 20);

public sealed record RecipeRequest(Guid ActorId, Guid OrganizationId, Guid RecipeId);

public sealed record MealRequest(Guid ActorId, Guid OrganizationId, Guid CampId, Guid MealId);

public sealed record Ingredient(
    Guid Id,
    Guid OrganizationId,
    string Name,
    bool IsMerged,
    Guid? MergedIntoIngredientId,
    long Version);

public sealed record CreateIngredient(Guid ActorId, Guid OrganizationId, string Name);

public sealed record RenameIngredient(
    Guid ActorId,
    Guid OrganizationId,
    Guid IngredientId,
    string Name,
    long ExpectedVersion);

public sealed record IngredientMergeRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid SourceIngredientId,
    Guid TargetIngredientId);

public sealed record MergeIngredients(
    Guid ActorId,
    Guid OrganizationId,
    Guid SourceIngredientId,
    Guid TargetIngredientId,
    long ExpectedSourceVersion,
    long ExpectedTargetVersion);

public sealed record IngredientMergePreview(
    Ingredient Source,
    Ingredient Target,
    IReadOnlyList<RecipeSummary> AffectedRecipes);

public sealed record IngredientMergeResult(
    Ingredient Target,
    IReadOnlyList<Guid> RevisedRecipeIds);

public sealed record RecipeIngredientInput(Guid IngredientId, Quantity Quantity, string? Note = null);

public sealed record RecipeContent(
    string Name,
    string Description,
    string Preparation,
    int BasePortions,
    IReadOnlyList<RecipeIngredientInput> Ingredients,
    IReadOnlyList<string> DietaryTags,
    string? AllergenNotes = null,
    string? KitchenNotes = null);

public sealed record CreateRecipe(Guid ActorId, Guid OrganizationId, RecipeContent Content);

public sealed record ReviseRecipe(
    Guid ActorId,
    Guid OrganizationId,
    Guid RecipeId,
    long ExpectedVersion,
    RecipeContent Content);

public sealed record RecipeSummary(
    Guid Id,
    Guid OrganizationId,
    string Name,
    int BasePortions,
    int CurrentVersionNumber,
    long Version);

public sealed record Recipe(
    Guid Id,
    Guid OrganizationId,
    RecipeVersion CurrentVersion,
    long Version);

public sealed record RecipeVersion(
    Guid Id,
    int Number,
    string Name,
    string Description,
    string Preparation,
    int BasePortions,
    IReadOnlyList<RecipeIngredient> Ingredients,
    IReadOnlyList<string> DietaryTags,
    string? AllergenNotes,
    string? KitchenNotes,
    DateTimeOffset CreatedAt);

public sealed record RecipeIngredient(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    Quantity Quantity,
    string? Note);

public sealed record CreateMeal(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    int? PortionOverride,
    Guid? ScheduleEntryId,
    IReadOnlyList<Guid> RecipeIds);

public sealed record ReviseMeal(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid MealId,
    string Name,
    int? PortionOverride,
    Guid? ScheduleEntryId,
    long ExpectedVersion);

public sealed record AddRecipeSnapshot(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid MealId,
    Guid RecipeId,
    long ExpectedVersion);

public sealed record RemoveRecipeSnapshot(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid MealId,
    Guid RecipeSnapshotId,
    long ExpectedVersion);

public sealed record RefreshRecipeSnapshot(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid MealId,
    Guid RecipeSnapshotId,
    long ExpectedVersion);

public sealed record MealSummary(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    int EffectivePortions,
    Guid? ScheduleEntryId,
    int RecipeCount,
    long Version);

public sealed record Meal(
    Guid Id,
    Guid OrganizationId,
    Guid CampId,
    string Name,
    int CampDefaultPortions,
    int? PortionOverride,
    int EffectivePortions,
    Guid? ScheduleEntryId,
    IReadOnlyList<RecipeSnapshot> RecipeSnapshots,
    long Version);

public sealed record RecipeSnapshot(
    Guid Id,
    Guid SourceRecipeId,
    int SourceRecipeVersionNumber,
    int LatestRecipeVersionNumber,
    bool RefreshAvailable,
    string Name,
    string Description,
    string Preparation,
    int BasePortions,
    IReadOnlyList<SnapshotIngredient> Ingredients,
    IReadOnlyList<string> DietaryTags,
    string? AllergenNotes,
    string? KitchenNotes,
    DateTimeOffset CapturedAt);

public sealed record SnapshotIngredient(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    Quantity BaseQuantity,
    Quantity ScaledQuantity,
    string? Note);

public sealed record MealShoppingDraft(
    Guid MealId,
    string MealName,
    int EffectivePortions,
    long MealVersion,
    IReadOnlyList<MealShoppingLine> Lines);

public sealed record MealShoppingLine(
    Guid RecipeSnapshotId,
    Guid SnapshotIngredientId,
    Guid SourceRecipeId,
    int SourceRecipeVersionNumber,
    string SourceLabel,
    string IngredientName,
    Quantity SuggestedQuantity,
    QuantityDimension Dimension,
    IReadOnlyList<MeasurementUnit> CompatibleUnits);

public sealed record Quantity
{
    public Quantity(decimal value, MeasurementUnit unit, string? countUnitName = null)
    {
        if (value <= 0m)
        {
            throw new CateringRuleException("invalid_quantity", "Die Menge muss größer als null sein.");
        }

        if (unit == MeasurementUnit.NamedCount)
        {
            if (string.IsNullOrWhiteSpace(countUnitName))
            {
                throw new CateringRuleException(
                    "count_unit_required",
                    "Für diese Zähleinheit ist eine Bezeichnung erforderlich.");
            }

            CountUnitName = NormalizeCountName(countUnitName);
        }
        else if (!string.IsNullOrWhiteSpace(countUnitName))
        {
            throw new CateringRuleException(
                "count_unit_not_allowed",
                "Eine Bezeichnung ist nur für fachliche Zähleinheiten erlaubt.");
        }

        Value = value;
        Unit = unit;
    }

    public decimal Value { get; }

    public MeasurementUnit Unit { get; }

    public string? CountUnitName { get; }

    public QuantityDimension Dimension => Unit switch
    {
        MeasurementUnit.Gram or MeasurementUnit.Kilogram => QuantityDimension.Mass,
        MeasurementUnit.Milliliter or MeasurementUnit.Liter => QuantityDimension.Volume,
        MeasurementUnit.Piece or MeasurementUnit.NamedCount => QuantityDimension.Count,
        _ => throw new UnreachableException()
    };

    public Quantity ConvertTo(MeasurementUnit targetUnit, string? targetCountUnitName = null)
    {
        var resolvedTargetName = targetUnit == MeasurementUnit.NamedCount
            ? NormalizeCountName(targetCountUnitName ?? CountUnitName ?? string.Empty)
            : null;

        if (!IsCompatible(targetUnit, resolvedTargetName))
        {
            throw new CateringRuleException(
                "incompatible_unit",
                "Diese Einheiten können nicht ineinander umgerechnet werden.");
        }

        var baseValue = Value * Factor(Unit);
        return new Quantity(baseValue / Factor(targetUnit), targetUnit, resolvedTargetName);
    }

    public IReadOnlyList<MeasurementUnit> CompatibleUnits => Dimension switch
    {
        QuantityDimension.Mass => [MeasurementUnit.Gram, MeasurementUnit.Kilogram],
        QuantityDimension.Volume => [MeasurementUnit.Milliliter, MeasurementUnit.Liter],
        QuantityDimension.Count when Unit == MeasurementUnit.Piece => [MeasurementUnit.Piece],
        QuantityDimension.Count => [MeasurementUnit.NamedCount],
        _ => throw new UnreachableException()
    };

    private bool IsCompatible(MeasurementUnit targetUnit, string? targetCountUnitName)
    {
        var targetDimension = targetUnit switch
        {
            MeasurementUnit.Gram or MeasurementUnit.Kilogram => QuantityDimension.Mass,
            MeasurementUnit.Milliliter or MeasurementUnit.Liter => QuantityDimension.Volume,
            MeasurementUnit.Piece or MeasurementUnit.NamedCount => QuantityDimension.Count,
            _ => throw new UnreachableException()
        };

        if (Dimension != targetDimension)
        {
            return false;
        }

        return Dimension != QuantityDimension.Count ||
            (Unit == MeasurementUnit.Piece && targetUnit == MeasurementUnit.Piece) ||
            (Unit == MeasurementUnit.NamedCount &&
             targetUnit == MeasurementUnit.NamedCount &&
             string.Equals(CountUnitName, targetCountUnitName, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal Factor(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Gram or MeasurementUnit.Milliliter or MeasurementUnit.Piece or MeasurementUnit.NamedCount => 1m,
        MeasurementUnit.Kilogram or MeasurementUnit.Liter => 1000m,
        _ => throw new UnreachableException()
    };

    private static string NormalizeCountName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).Trim();
        var builder = new StringBuilder(normalized.Length);
        var previousWasWhitespace = false;

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }
}

public enum MeasurementUnit
{
    Gram,
    Kilogram,
    Milliliter,
    Liter,
    Piece,
    NamedCount
}

public enum QuantityDimension
{
    Mass,
    Volume,
    Count
}

public sealed class CateringRuleException(string errorCode, string message) : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

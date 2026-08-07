using Catering.Contracts;

namespace Catering.Implementation;

internal sealed class IngredientEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public Guid? MergedIntoIngredientId { get; set; }

    public long Version { get; set; } = 1;
}

internal sealed class RecipeEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public long Version { get; set; } = 1;

    public List<RecipeVersionEntity> Versions { get; } = [];
}

internal sealed class RecipeVersionEntity
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public Guid OrganizationId { get; set; }

    public int Number { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public required string Preparation { get; set; }

    public int BasePortions { get; set; }

    public string[] DietaryTags { get; set; } = [];

    public string? AllergenNotes { get; set; }

    public string? KitchenNotes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<RecipeIngredientEntity> Ingredients { get; } = [];
}

internal sealed class RecipeIngredientEntity
{
    public Guid Id { get; set; }

    public Guid RecipeVersionId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid IngredientId { get; set; }

    public required string IngredientName { get; set; }

    public decimal Amount { get; set; }

    public MeasurementUnit Unit { get; set; }

    public string? CountUnitName { get; set; }

    public string? Note { get; set; }
}

internal sealed class MealEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public required string Name { get; set; }

    public int? PortionOverride { get; set; }

    public Guid? ScheduleEntryId { get; set; }

    public long Version { get; set; } = 1;

    public List<RecipeSnapshotEntity> RecipeSnapshots { get; } = [];
}

internal sealed class RecipeSnapshotEntity
{
    public Guid Id { get; set; }

    public Guid MealId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public Guid SourceRecipeId { get; set; }

    public int SourceRecipeVersionNumber { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public required string Preparation { get; set; }

    public int BasePortions { get; set; }

    public string[] DietaryTags { get; set; } = [];

    public string? AllergenNotes { get; set; }

    public string? KitchenNotes { get; set; }

    public DateTimeOffset CapturedAt { get; set; }

    public bool IsCurrent { get; set; } = true;

    public List<SnapshotIngredientEntity> Ingredients { get; } = [];
}

internal sealed class SnapshotIngredientEntity
{
    public Guid Id { get; set; }

    public Guid RecipeSnapshotId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public Guid IngredientId { get; set; }

    public required string IngredientName { get; set; }

    public decimal Amount { get; set; }

    public MeasurementUnit Unit { get; set; }

    public string? CountUnitName { get; set; }

    public string? Note { get; set; }
}

using Microsoft.EntityFrameworkCore;

namespace Catering.Implementation;

public sealed class CateringDbContext(DbContextOptions<CateringDbContext> options) : DbContext(options)
{
    internal DbSet<IngredientEntity> Ingredients => Set<IngredientEntity>();

    internal DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();

    internal DbSet<RecipeVersionEntity> RecipeVersions => Set<RecipeVersionEntity>();

    internal DbSet<RecipeIngredientEntity> RecipeIngredients => Set<RecipeIngredientEntity>();

    internal DbSet<MealEntity> Meals => Set<MealEntity>();

    internal DbSet<RecipeSnapshotEntity> RecipeSnapshots => Set<RecipeSnapshotEntity>();

    internal DbSet<SnapshotIngredientEntity> SnapshotIngredients => Set<SnapshotIngredientEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catering");

        modelBuilder.Entity<IngredientEntity>(entity =>
        {
            entity.ToTable("ingredients");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.MergedIntoIngredientId).HasColumnName("merged_into_ingredient_id");
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.NormalizedName).HasMaxLength(160);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.OrganizationId, item.NormalizedName }).IsUnique();
        });

        modelBuilder.Entity<RecipeEntity>(entity =>
        {
            entity.ToTable("recipes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.OrganizationId);
            entity.HasMany(item => item.Versions)
                .WithOne()
                .HasForeignKey(item => item.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeVersionEntity>(entity =>
        {
            entity.ToTable("recipe_versions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RecipeId).HasColumnName("recipe_id");
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.Description).HasMaxLength(4000);
            entity.Property(item => item.Preparation).HasMaxLength(16000);
            entity.Property(item => item.DietaryTags).HasColumnType("text[]");
            entity.Property(item => item.AllergenNotes).HasMaxLength(4000);
            entity.Property(item => item.KitchenNotes).HasMaxLength(4000);
            entity.HasIndex(item => new { item.RecipeId, item.Number }).IsUnique();
            entity.HasIndex(item => item.OrganizationId);
            entity.HasMany(item => item.Ingredients)
                .WithOne()
                .HasForeignKey(item => item.RecipeVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeIngredientEntity>(entity =>
        {
            entity.ToTable("recipe_ingredients");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RecipeVersionId).HasColumnName("recipe_version_id");
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.IngredientId).HasColumnName("ingredient_id");
            entity.Property(item => item.IngredientName).HasMaxLength(160);
            entity.Property(item => item.Amount).HasPrecision(18, 6);
            entity.Property(item => item.Unit).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.CountUnitName).HasMaxLength(80);
            entity.Property(item => item.Note).HasMaxLength(500);
            entity.HasIndex(item => item.OrganizationId);
        });

        modelBuilder.Entity<MealEntity>(entity =>
        {
            entity.ToTable("meals");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.ScheduleEntryId).HasColumnName("schedule_entry_id");
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.OrganizationId, item.CampId });
            entity.HasMany(item => item.RecipeSnapshots)
                .WithOne()
                .HasForeignKey(item => item.MealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeSnapshotEntity>(entity =>
        {
            entity.ToTable("recipe_snapshots");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MealId).HasColumnName("meal_id");
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.SourceRecipeId).HasColumnName("source_recipe_id");
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.Description).HasMaxLength(4000);
            entity.Property(item => item.Preparation).HasMaxLength(16000);
            entity.Property(item => item.DietaryTags).HasColumnType("text[]");
            entity.Property(item => item.AllergenNotes).HasMaxLength(4000);
            entity.Property(item => item.KitchenNotes).HasMaxLength(4000);
            entity.HasIndex(item => new { item.MealId, item.IsCurrent });
            entity.HasIndex(item => new { item.OrganizationId, item.CampId });
            entity.HasMany(item => item.Ingredients)
                .WithOne()
                .HasForeignKey(item => item.RecipeSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SnapshotIngredientEntity>(entity =>
        {
            entity.ToTable("snapshot_ingredients");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RecipeSnapshotId).HasColumnName("recipe_snapshot_id");
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.IngredientId).HasColumnName("ingredient_id");
            entity.Property(item => item.IngredientName).HasMaxLength(160);
            entity.Property(item => item.Amount).HasPrecision(18, 6);
            entity.Property(item => item.Unit).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.CountUnitName).HasMaxLength(80);
            entity.Property(item => item.Note).HasMaxLength(500);
            entity.HasIndex(item => new { item.OrganizationId, item.CampId });
        });
    }
}

using Logistics.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Implementation;

public sealed class LogisticsDbContext(DbContextOptions<LogisticsDbContext> options) : DbContext(options)
{
    internal DbSet<MaterialRequirementEntity> Materials => Set<MaterialRequirementEntity>();
    internal DbSet<MaterialResponsibilityEntity> MaterialResponsibilities => Set<MaterialResponsibilityEntity>();
    internal DbSet<ShoppingListEntity> ShoppingLists => Set<ShoppingListEntity>();
    internal DbSet<ShoppingItemEntity> ShoppingItems => Set<ShoppingItemEntity>();
    internal DbSet<ShoppingItemResponsibilityEntity> ShoppingItemResponsibilities => Set<ShoppingItemResponsibilityEntity>();
    internal DbSet<ShoppingCheckEventEntity> ShoppingCheckEvents => Set<ShoppingCheckEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("logistics");
        ConfigureMaterial(modelBuilder.Entity<MaterialRequirementEntity>());
        ConfigureMaterialResponsibility(modelBuilder.Entity<MaterialResponsibilityEntity>());
        ConfigureShoppingList(modelBuilder.Entity<ShoppingListEntity>());
        ConfigureShoppingItem(modelBuilder.Entity<ShoppingItemEntity>());
        ConfigureShoppingResponsibility(modelBuilder.Entity<ShoppingItemResponsibilityEntity>());
        ConfigureAudit(modelBuilder.Entity<ShoppingCheckEventEntity>());
    }

    private static void ConfigureMaterial(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<MaterialRequirementEntity> entity)
    {
        entity.ToTable("material_requirements", table =>
        {
            table.HasCheckConstraint("CK_material_quantity", "\"QuantityValue\" > 0");
            table.HasCheckConstraint("CK_material_version", "\"Version\" > 0");
            table.HasCheckConstraint("CK_material_custom_unit", "(\"QuantityUnit\" = 5 AND \"CustomUnitName\" IS NOT NULL) OR (\"QuantityUnit\" <> 5 AND \"CustomUnitName\" IS NULL)");
        });
        entity.HasKey(x => x.Id);
        entity.HasAlternateKey(x => new { x.OrganizationId, x.CampId, x.Id });
        TenantColumns(entity);
        entity.Property(x => x.Name).HasMaxLength(200);
        entity.Property(x => x.Description).HasMaxLength(4000);
        entity.Property(x => x.QuantityValue).HasPrecision(18, 6);
        entity.Property(x => x.CustomUnitName).HasMaxLength(80);
        entity.Property(x => x.ProcurementSource).HasMaxLength(240);
        entity.Property(x => x.Note).HasMaxLength(2000);
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasIndex(x => new { x.OrganizationId, x.CampId, x.Status });
        entity.HasIndex(x => new { x.OrganizationId, x.CampId, x.ScheduleEntryId });
        entity.HasIndex(x => new { x.OrganizationId, x.CampId, x.PurgeAt });
    }

    private static void ConfigureMaterialResponsibility(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<MaterialResponsibilityEntity> entity)
    {
        entity.ToTable("material_responsibilities");
        entity.HasKey(x => new { x.MaterialRequirementId, x.UserId });
        TenantColumns(entity);
        entity.Property(x => x.MaterialRequirementId).HasColumnName("material_requirement_id");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.HasOne<MaterialRequirementEntity>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.CampId, x.MaterialRequirementId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.CampId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => new { x.OrganizationId, x.CampId, x.UserId });
    }

    private static void ConfigureShoppingList(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ShoppingListEntity> entity)
    {
        entity.ToTable("shopping_lists", table =>
        {
            table.HasCheckConstraint("CK_shopping_lists_version", "\"Version\" > 0");
            table.HasCheckConstraint("CK_shopping_lists_change_sequence", "\"ChangeSequence\" > 0");
        });
        entity.HasKey(x => x.Id);
        entity.HasAlternateKey(x => new { x.OrganizationId, x.CampId, x.Id });
        TenantColumns(entity);
        entity.Property(x => x.Name).HasMaxLength(160);
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasIndex(x => new { x.OrganizationId, x.CampId, x.Name });
    }

    private static void ConfigureShoppingItem(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ShoppingItemEntity> entity)
    {
        entity.ToTable("shopping_items", table =>
        {
            table.HasCheckConstraint("CK_shopping_items_quantity", "\"QuantityValue\" > 0");
            table.HasCheckConstraint("CK_shopping_items_version", "\"Version\" > 0");
            table.HasCheckConstraint("CK_shopping_items_custom_unit", "(\"QuantityUnit\" = 5 AND \"CustomUnitName\" IS NOT NULL) OR (\"QuantityUnit\" <> 5 AND \"CustomUnitName\" IS NULL)");
            table.HasCheckConstraint("CK_shopping_items_check_state", "(\"IsChecked\" AND \"checked_by_user_id\" IS NOT NULL AND \"CheckedAt\" IS NOT NULL) OR (NOT \"IsChecked\" AND \"checked_by_user_id\" IS NULL AND \"CheckedAt\" IS NULL)");
            table.HasCheckConstraint("CK_shopping_items_source", "(\"SourceKind\" = 0 AND \"CateringMealId\" IS NULL AND \"CateringRecipeSnapshotId\" IS NULL AND \"CateringSnapshotIngredientId\" IS NULL AND \"CateringSourceRecipeId\" IS NULL AND \"CateringSourceRecipeVersionNumber\" IS NULL AND \"MaterialRequirementId\" IS NULL AND \"MaterialRequirementVersion\" IS NULL) OR (\"SourceKind\" = 1 AND \"CateringMealId\" IS NOT NULL AND \"CateringRecipeSnapshotId\" IS NOT NULL AND \"CateringSnapshotIngredientId\" IS NOT NULL AND \"CateringSourceRecipeId\" IS NOT NULL AND \"CateringSourceRecipeVersionNumber\" > 0 AND \"MaterialRequirementId\" IS NULL AND \"MaterialRequirementVersion\" IS NULL) OR (\"SourceKind\" = 2 AND \"MaterialRequirementId\" IS NOT NULL AND \"MaterialRequirementVersion\" > 0 AND \"CateringMealId\" IS NULL AND \"CateringRecipeSnapshotId\" IS NULL AND \"CateringSnapshotIngredientId\" IS NULL AND \"CateringSourceRecipeId\" IS NULL AND \"CateringSourceRecipeVersionNumber\" IS NULL)");
        });
        entity.HasKey(x => x.Id);
        entity.HasAlternateKey(x => new { x.OrganizationId, x.CampId, x.ShoppingListId, x.Id });
        TenantColumns(entity);
        entity.Property(x => x.ShoppingListId).HasColumnName("shopping_list_id");
        entity.Property(x => x.Name).HasMaxLength(200);
        entity.Property(x => x.QuantityValue).HasPrecision(18, 6);
        entity.Property(x => x.CustomUnitName).HasMaxLength(80);
        entity.Property(x => x.Store).HasMaxLength(160);
        entity.Property(x => x.Note).HasMaxLength(2000);
        entity.Property(x => x.SourceLabel).HasMaxLength(240);
        entity.Property(x => x.CheckedByUserId).HasColumnName("checked_by_user_id");
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasOne<ShoppingListEntity>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.CampId, x.ShoppingListId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.CampId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => new { x.OrganizationId, x.CampId, x.ShoppingListId, x.IsChecked });
    }

    private static void ConfigureShoppingResponsibility(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ShoppingItemResponsibilityEntity> entity)
    {
        entity.ToTable("shopping_item_responsibilities");
        entity.HasKey(x => new { x.ShoppingItemId, x.UserId });
        TenantColumns(entity);
        entity.Property(x => x.ShoppingListId).HasColumnName("shopping_list_id");
        entity.Property(x => x.ShoppingItemId).HasColumnName("shopping_item_id");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.HasOne<ShoppingItemEntity>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.CampId, x.ShoppingListId, x.ShoppingItemId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.CampId, x.ShoppingListId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => new { x.OrganizationId, x.CampId, x.UserId });
    }

    private static void ConfigureAudit(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ShoppingCheckEventEntity> entity)
    {
        entity.ToTable("shopping_check_events", table => table.HasCheckConstraint("CK_shopping_check_event_version", "\"ResultingItemVersion\" > 1"));
        entity.HasKey(x => x.Id);
        TenantColumns(entity);
        entity.Property(x => x.ShoppingListId).HasColumnName("shopping_list_id");
        entity.Property(x => x.ShoppingItemId).HasColumnName("shopping_item_id");
        entity.Property(x => x.ActorId).HasColumnName("actor_id");
        entity.HasIndex(x => new { x.OrganizationId, x.CampId, x.ShoppingItemId, x.OccurredAt });
    }

    private static void TenantColumns<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity) where TEntity : class, ITenantCampEntity
    {
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.CampId).HasColumnName("camp_id");
    }
}

internal interface ITenantCampEntity { Guid OrganizationId { get; set; } Guid CampId { get; set; } }

internal sealed class MaterialRequirementEntity : ITenantCampEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CampId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal QuantityValue { get; set; }
    public LogisticsUnit QuantityUnit { get; set; }
    public string? CustomUnitName { get; set; }
    public string? ProcurementSource { get; set; }
    public string? Note { get; set; }
    public ProcurementStatus Status { get; set; }
    public Guid? ScheduleEntryId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? PurgeAt { get; set; }
    public long Version { get; set; } = 1;
}
internal sealed class MaterialResponsibilityEntity : ITenantCampEntity { public Guid MaterialRequirementId { get; set; } public Guid UserId { get; set; } public Guid OrganizationId { get; set; } public Guid CampId { get; set; } }
internal sealed class ShoppingListEntity : ITenantCampEntity { public Guid Id { get; set; } public Guid OrganizationId { get; set; } public Guid CampId { get; set; } public required string Name { get; set; } public long Version { get; set; } = 1; public long ChangeSequence { get; set; } = 1; }
internal sealed class ShoppingItemEntity : ITenantCampEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CampId { get; set; }
    public Guid ShoppingListId { get; set; }
    public required string Name { get; set; }
    public decimal QuantityValue { get; set; }
    public LogisticsUnit QuantityUnit { get; set; }
    public string? CustomUnitName { get; set; }
    public string? Store { get; set; }
    public string? Note { get; set; }
    public ShoppingSourceKind SourceKind { get; set; }
    public required string SourceLabel { get; set; }
    public Guid? CateringMealId { get; set; }
    public Guid? CateringRecipeSnapshotId { get; set; }
    public Guid? CateringSnapshotIngredientId { get; set; }
    public Guid? CateringSourceRecipeId { get; set; }
    public int? CateringSourceRecipeVersionNumber { get; set; }
    public Guid? MaterialRequirementId { get; set; }
    public long? MaterialRequirementVersion { get; set; }
    public bool IsChecked { get; set; }
    public Guid? CheckedByUserId { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }
    public long Version { get; set; } = 1;
}
internal sealed class ShoppingItemResponsibilityEntity : ITenantCampEntity { public Guid ShoppingListId { get; set; } public Guid ShoppingItemId { get; set; } public Guid UserId { get; set; } public Guid OrganizationId { get; set; } public Guid CampId { get; set; } }
internal sealed class ShoppingCheckEventEntity : ITenantCampEntity { public Guid Id { get; set; } public Guid OrganizationId { get; set; } public Guid CampId { get; set; } public Guid ShoppingListId { get; set; } public Guid ShoppingItemId { get; set; } public ShoppingCheckAction Action { get; set; } public Guid ActorId { get; set; } public DateTimeOffset OccurredAt { get; set; } public long ResultingItemVersion { get; set; } }

using Microsoft.EntityFrameworkCore;

namespace Activity.Implementation;

public sealed class ActivityDbContext(DbContextOptions<ActivityDbContext> options) : DbContext(options)
{
    internal DbSet<ActivityEventEntity> ActivityEvents => Set<ActivityEventEntity>();

    internal DbSet<SearchDocumentEntity> SearchDocuments => Set<SearchDocumentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("activity");
        modelBuilder.Entity<ActivityEventEntity>(entity =>
        {
            entity.ToTable("activity_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ActorId).HasColumnName("actor_id");
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.ObjectType).HasMaxLength(80);
            entity.Property(item => item.ObjectId).HasColumnName("object_id");
            entity.Property(item => item.Title).HasMaxLength(160);
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.Timestamp });
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.ObjectType, item.ObjectId });
        });

        modelBuilder.Entity<SearchDocumentEntity>(entity =>
        {
            entity.ToTable("search_documents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.ObjectType).HasMaxLength(80);
            entity.Property(item => item.ObjectId).HasColumnName("object_id");
            entity.Property(item => item.Title).HasMaxLength(160);
            entity.Property(item => item.SearchText).HasMaxLength(2000);
            entity.Property(item => item.MetadataJson).HasColumnType("jsonb");
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.ObjectType, item.ObjectId }).IsUnique();
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.IsRemoved, item.ObjectType });
        });
    }
}

using Knowledge.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Knowledge.Implementation;

public sealed class KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options) : DbContext(options)
{
    internal DbSet<NoteEntity> Notes => Set<NoteEntity>();

    internal DbSet<NoteTagEntity> NoteTags => Set<NoteTagEntity>();

    internal DbSet<NoteLinkEntity> NoteLinks => Set<NoteLinkEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("knowledge");
        modelBuilder.Entity<NoteEntity>(entity =>
        {
            entity.ToTable("notes");
            entity.HasKey(item => item.Id);
            entity.HasAlternateKey(item => new { item.Id, item.OrganizationId, item.CampId });
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.Title).HasMaxLength(160);
            entity.Property(item => item.Markdown).HasMaxLength(50_000);
            entity.Property(item => item.State).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.CreatedBy).HasColumnName("created_by");
            entity.Property(item => item.UpdatedBy).HasColumnName("updated_by");
            entity.Property(item => item.TrashedBy).HasColumnName("trashed_by");
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new
            {
                item.OrganizationId,
                item.CampId,
                item.State,
                item.IsPinned,
                item.UpdatedAt
            });
            entity.HasIndex(item => new { item.State, item.PurgeAfter });
            entity.HasMany(item => item.Tags)
                .WithOne()
                .HasForeignKey(item => new { item.NoteId, item.OrganizationId, item.CampId })
                .HasPrincipalKey(item => new { item.Id, item.OrganizationId, item.CampId })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Links)
                .WithOne()
                .HasForeignKey(item => new { item.NoteId, item.OrganizationId, item.CampId })
                .HasPrincipalKey(item => new { item.Id, item.OrganizationId, item.CampId })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NoteTagEntity>(entity =>
        {
            entity.ToTable("note_tags");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.NoteId).HasColumnName("note_id");
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.DisplayName).HasMaxLength(40);
            entity.Property(item => item.NormalizedName).HasMaxLength(40);
            entity.HasIndex(item => new { item.NoteId, item.NormalizedName }).IsUnique();
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.NormalizedName });
        });

        modelBuilder.Entity<NoteLinkEntity>(entity =>
        {
            entity.ToTable("note_links");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.NoteId).HasColumnName("note_id");
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.TargetType).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.TargetId).HasColumnName("target_id");
            entity.Property(item => item.TargetTitleSnapshot).HasMaxLength(160);
            entity.HasIndex(item => new { item.NoteId, item.TargetType, item.TargetId }).IsUnique();
            entity.HasIndex(item => new { item.OrganizationId, item.CampId });
        });
    }
}

using Microsoft.EntityFrameworkCore;
using Spiritual.Contracts;

namespace Spiritual.Implementation;

public sealed class SpiritualDbContext(DbContextOptions<SpiritualDbContext> options)
    : DbContext(options)
{
    internal DbSet<DevotionEntity> Devotions => Set<DevotionEntity>();

    internal DbSet<BibleSnapshotEntity> BibleSnapshots => Set<BibleSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("spiritual");
        modelBuilder.Entity<DevotionEntity>(entity =>
        {
            entity.ToTable("devotions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.Topic).HasMaxLength(200);
            entity.Property(item => item.BibleReference).HasMaxLength(160);
            entity.Property(item => item.Translation).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.CoreMessage).HasMaxLength(1000);
            entity.Property(item => item.ResponsibleUserIds).HasColumnType("uuid[]");
            entity.Property(item => item.MaterialNotes).HasMaxLength(4000);
            entity.Property(item => item.ScheduleEntryId).HasColumnName("schedule_entry_id");
            entity.Property(item => item.CurrentBibleSnapshotId).HasColumnName("current_bible_snapshot_id");
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.DeletedAt });
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.ScheduleEntryId });
            entity.HasOne<BibleSnapshotEntity>()
                .WithMany()
                .HasForeignKey(item => item.CurrentBibleSnapshotId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BibleSnapshotEntity>(entity =>
        {
            entity.ToTable("bible_snapshots");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.DevotionId).HasColumnName("devotion_id");
            entity.Property(item => item.Reference).HasMaxLength(160);
            entity.Property(item => item.TechnicalTranslationId).HasMaxLength(32);
            entity.Property(item => item.TranslationDisplayName).HasMaxLength(160);
            entity.Property(item => item.License).HasMaxLength(500);
            entity.Property(item => item.Attribution).HasMaxLength(2000);
            entity.Property(item => item.Origin).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.DevotionId, item.RetrievedAt });
        });
    }
}

internal sealed class DevotionEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public required string Topic { get; set; }

    public required string BibleReference { get; set; }

    public BibleTranslation Translation { get; set; }

    public required string CoreMessage { get; set; }

    public required string MarkdownContent { get; set; }

    public Guid[] ResponsibleUserIds { get; set; } = [];

    public required string MaterialNotes { get; set; }

    public Guid? ScheduleEntryId { get; set; }

    public Guid? CurrentBibleSnapshotId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public long Version { get; set; } = 1;
}

internal sealed class BibleSnapshotEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public Guid DevotionId { get; set; }

    public required string Reference { get; set; }

    public required string TextExcerpt { get; set; }

    public required string TechnicalTranslationId { get; set; }

    public required string TranslationDisplayName { get; set; }

    public required string License { get; set; }

    public required string Attribution { get; set; }

    public DateTimeOffset RetrievedAt { get; set; }

    public BibleSnapshotOrigin Origin { get; set; }
}

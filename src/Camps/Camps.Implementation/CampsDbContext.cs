using Camps.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Camps.Implementation;

public sealed class CampsDbContext(DbContextOptions<CampsDbContext> options) : DbContext(options)
{
    internal DbSet<CampEntity> Camps => Set<CampEntity>();

    internal DbSet<ScheduleEntryEntity> ScheduleEntries => Set<ScheduleEntryEntity>();

    internal DbSet<ScheduleResponsibilityEntity> ScheduleResponsibilities =>
        Set<ScheduleResponsibilityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("camps");

        modelBuilder.Entity<CampEntity>(entity =>
        {
            entity.ToTable("camps", table =>
            {
                table.HasCheckConstraint("CK_camps_dates", "\"EndsOn\" >= \"StartsOn\"");
                table.HasCheckConstraint("CK_camps_default_portions", "\"DefaultPortions\" > 0");
                table.HasCheckConstraint("CK_camps_version", "\"Version\" > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.Slug).HasMaxLength(80);
            entity.Property(item => item.Description).HasMaxLength(4000);
            entity.Property(item => item.TimeZoneId).HasMaxLength(120);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            entity.HasIndex(item => new { item.OrganizationId, item.Slug }).IsUnique();
            entity.HasIndex(item => new { item.OrganizationId, item.Status, item.StartsOn });
        });

        modelBuilder.Entity<ScheduleEntryEntity>(entity =>
        {
            entity.ToTable("schedule_entries", table =>
            {
                table.HasCheckConstraint(
                    "CK_schedule_entries_timing",
                    "(\"IsAllDay\" AND \"StartsAtUtc\" IS NULL AND \"EndsAtUtc\" IS NULL "
                    + "AND \"StartDate\" IS NOT NULL AND \"EndDateExclusive\" > \"StartDate\") "
                    + "OR (NOT \"IsAllDay\" AND \"StartsAtUtc\" IS NOT NULL "
                    + "AND \"EndsAtUtc\" > \"StartsAtUtc\" AND \"StartDate\" IS NULL "
                    + "AND \"EndDateExclusive\" IS NULL)");
                table.HasCheckConstraint("CK_schedule_entries_version", "\"Version\" > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.Title).HasMaxLength(200);
            entity.Property(item => item.Description).HasMaxLength(8000);
            entity.Property(item => item.Location).HasMaxLength(240);
            entity.Property(item => item.Category).HasMaxLength(80);
            entity.Property(item => item.Audience).HasMaxLength(160);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasAlternateKey(item => new { item.OrganizationId, item.CampId, item.Id });
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.StartsAtUtc });
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.StartDate });
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.PurgeAt });
            entity.HasOne<CampEntity>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.CampId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduleResponsibilityEntity>(entity =>
        {
            entity.ToTable("schedule_responsibilities");
            entity.HasKey(item => new { item.ScheduleEntryId, item.UserId });
            entity.Property(item => item.ScheduleEntryId).HasColumnName("schedule_entry_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.UserId });
            entity.HasOne<ScheduleEntryEntity>()
                .WithMany()
                .HasForeignKey(item => new
                {
                    item.OrganizationId,
                    item.CampId,
                    item.ScheduleEntryId
                })
                .HasPrincipalKey(item => new { item.OrganizationId, item.CampId, item.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class CampEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public string? Description { get; set; }

    public DateOnly StartsOn { get; set; }

    public DateOnly EndsOn { get; set; }

    public required string TimeZoneId { get; set; }

    public int DefaultPortions { get; set; }

    public CampStatus Status { get; set; }

    public long Version { get; set; } = 1;

}

internal sealed class ScheduleEntryEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public bool IsAllDay { get; set; }

    public DateTimeOffset? StartsAtUtc { get; set; }

    public DateTimeOffset? EndsAtUtc { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDateExclusive { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public string? Location { get; set; }

    public required string Category { get; set; }

    public ScheduleEntryStatus Status { get; set; }

    public string? Audience { get; set; }

    public long Version { get; set; } = 1;

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset? PurgeAt { get; set; }
}

internal sealed class ScheduleResponsibilityEntity
{
    public Guid ScheduleEntryId { get; set; }

    public Guid UserId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }
}

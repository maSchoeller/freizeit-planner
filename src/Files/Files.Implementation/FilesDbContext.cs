using Files.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Files.Implementation;

public sealed class FilesDbContext(DbContextOptions<FilesDbContext> options) : DbContext(options)
{
    internal DbSet<AttachmentEntity> Attachments => Set<AttachmentEntity>();

    internal DbSet<AttachmentReadGrantEntity> ReadGrants => Set<AttachmentReadGrantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("files");
        modelBuilder.Entity<AttachmentEntity>(entity =>
        {
            entity.ToTable("attachments", table =>
            {
                table.HasCheckConstraint(
                    "CK_attachments_owner_scope",
                    "(\"OwnerType\" = 'Recipe' AND camp_id IS NULL AND \"QuotaScope\" = 'OrganizationRecipeLibrary') OR "
                    + "(\"OwnerType\" <> 'Recipe' AND camp_id IS NOT NULL AND \"QuotaScope\" = 'Camp')");
                table.HasCheckConstraint(
                    "CK_attachments_size",
                    "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 10485760");
                table.HasCheckConstraint(
                    "CK_attachments_lifecycle",
                    "(\"State\" IN ('PendingUpload', 'Available') AND \"DeletedAt\" IS NULL AND \"PurgeAt\" IS NULL) OR "
                    + "(\"State\" = 'Deleted' AND \"DeletedAt\" IS NOT NULL AND \"PurgeAt\" > \"DeletedAt\")");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.OwnerType).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.OwnerId).HasColumnName("owner_id");
            entity.Property(item => item.QuotaScope).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.BlobName).HasMaxLength(128);
            entity.Property(item => item.OriginalFileName).HasMaxLength(255);
            entity.Property(item => item.MediaType).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.ContentType).HasMaxLength(100);
            entity.Property(item => item.State).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.BlobName).IsUnique();
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.OwnerType, item.OwnerId, item.State });
            entity.HasIndex(item => new { item.OrganizationId, item.CampId, item.QuotaScope, item.State });
            entity.HasIndex(item => new { item.State, item.PurgeAt });
        });
        modelBuilder.Entity<AttachmentReadGrantEntity>(entity =>
        {
            entity.ToTable("read_grants", table =>
            {
                table.HasCheckConstraint("CK_read_grants_hash", "octet_length(\"TokenHash\") = 32");
                table.HasCheckConstraint(
                    "CK_read_grants_expiry",
                    "\"ExpiresAt\" > \"CreatedAt\" AND \"ExpiresAt\" <= \"CreatedAt\" + interval '60 seconds'");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.AttachmentId).HasColumnName("attachment_id");
            entity.Property(item => item.ActorId).HasColumnName("actor_id");
            entity.Property(item => item.TokenHash).HasMaxLength(32);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.ActorId, item.ExpiresAt, item.UsedAt });
            entity.HasOne<AttachmentEntity>()
                .WithMany()
                .HasForeignKey(item => item.AttachmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class AttachmentEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid? CampId { get; set; }

    public AttachmentOwnerType OwnerType { get; set; }

    public Guid OwnerId { get; set; }

    public AttachmentQuotaScopeType QuotaScope { get; set; }

    public required string BlobName { get; set; }

    public required string OriginalFileName { get; set; }

    public AttachmentMediaType MediaType { get; set; }

    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public AttachmentLifecycleState State { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset? PurgeAt { get; set; }

    public long Version { get; set; } = 1;
}

internal sealed class AttachmentReadGrantEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid? CampId { get; set; }

    public Guid AttachmentId { get; set; }

    public Guid ActorId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

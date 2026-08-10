using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public bool IsSuperAdmin { get; set; }

    public bool IsPlatformAdmin { get; set; }

    public Identity.Contracts.AccountStatus AccountStatus { get; set; }

    public long Version { get; set; } = 1;

    public DateTimeOffset? DeletionScheduledAt { get; set; }

    public DateTimeOffset? ErasureStartedAt { get; set; }
}

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    internal DbSet<LoginChallengeEntity> LoginChallenges => Set<LoginChallengeEntity>();

    internal DbSet<LoginSessionEntity> LoginSessions => Set<LoginSessionEntity>();

    internal DbSet<LoginRateEventEntity> LoginRateEvents => Set<LoginRateEventEntity>();

    internal DbSet<PasswordResetTokenEntity> PasswordResetTokens => Set<PasswordResetTokenEntity>();

    internal DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

    internal DbSet<MembershipEntity> Memberships => Set<MembershipEntity>();

    internal DbSet<CampAssignmentEntity> CampAssignments => Set<CampAssignmentEntity>();

    internal DbSet<InvitationEntity> Invitations => Set<InvitationEntity>();

    internal DbSet<TransferableInvitationEntity> TransferableInvitations => Set<TransferableInvitationEntity>();

    internal DbSet<InvitationRegistrationEntity> InvitationRegistrations => Set<InvitationRegistrationEntity>();

    internal DbSet<EmailChangeChallengeEntity> EmailChangeChallenges => Set<EmailChangeChallengeEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(item => item.NormalizedEmail).IsUnique();
            entity.Property(item => item.FirstName).HasMaxLength(80);
            entity.Property(item => item.LastName).HasMaxLength(80);
            entity.Property(item => item.DisplayName).HasMaxLength(161);
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<LoginChallengeEntity>(entity =>
        {
            entity.ToTable("login_challenges");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.NormalizedEmail).IsUnique();
            entity.Property(item => item.NormalizedEmail).HasMaxLength(320);
            entity.Property(item => item.CodeHash).HasMaxLength(64);
        });
        builder.Entity<LoginSessionEntity>(entity =>
        {
            entity.ToTable("login_sessions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.ExpiresAt });
            entity.Property(item => item.IpAddress).HasMaxLength(64);
            entity.Property(item => item.RefreshTokenHash).HasMaxLength(64);
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        builder.Entity<LoginRateEventEntity>(entity =>
        {
            entity.ToTable("login_rate_events");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Partition, item.OccurredAt });
            entity.Property(item => item.Partition).HasMaxLength(400);
        });
        builder.Entity<PasswordResetTokenEntity>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.TokenHash).HasMaxLength(64);
            entity.HasIndex(item => new { item.UserId, item.ExpiresAt });
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Slug).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.Slug).HasMaxLength(80);
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        builder.Entity<MembershipEntity>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(item => new { item.OrganizationId, item.UserId });
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.HasIndex(item => new { item.UserId, item.IsActive });
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        builder.Entity<CampAssignmentEntity>(entity =>
        {
            entity.ToTable("camp_assignments");
            entity.HasKey(item => new { item.CampId, item.UserId });
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.HasIndex(item => new { item.OrganizationId, item.UserId });
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        builder.Entity<InvitationEntity>(entity =>
        {
            entity.ToTable("invitations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.NormalizedEmail).HasMaxLength(320);
            entity.Property(item => item.TokenHash).HasMaxLength(64);
            entity.HasIndex(item => new { item.OrganizationId, item.NormalizedEmail });
            entity.HasIndex(item => item.ExpiresAt);
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        builder.Entity<TransferableInvitationEntity>(entity =>
        {
            entity.ToTable("transferable_invitations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
            entity.Property(item => item.CampId).HasColumnName("camp_id");
            entity.Property(item => item.TokenHash).HasMaxLength(64);
            entity.Property(item => item.NewOrganizationName).HasMaxLength(160);
            entity.Property(item => item.NewOrganizationSlug).HasMaxLength(80);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => item.ExpiresAt);
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        builder.Entity<InvitationRegistrationEntity>(entity =>
        {
            entity.ToTable("invitation_registrations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.InvitationId).HasColumnName("invitation_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.TokenHash).HasMaxLength(64);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.InvitationId, item.UserId, item.ExpiresAt });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<TransferableInvitationEntity>()
                .WithMany()
                .HasForeignKey(item => item.InvitationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<EmailChangeChallengeEntity>(entity =>
        {
            entity.ToTable("email_change_challenges");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Email).HasMaxLength(320);
            entity.Property(item => item.NormalizedEmail).HasMaxLength(320);
            entity.Property(item => item.CodeHash).HasMaxLength(64);
            entity.HasIndex(item => item.NormalizedEmail);
            entity.HasIndex(item => item.ExpiresAt);
        });
    }
}

internal sealed class OrganizationEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public Identity.Contracts.OrganizationStatus Status { get; set; }

    public DateTimeOffset? DeletionScheduledAt { get; set; }

    public long Version { get; set; } = 1;
}

internal sealed class MembershipEntity
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public Identity.Contracts.TenantRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public long Version { get; set; } = 1;
}

internal sealed class CampAssignmentEntity
{
    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public Guid UserId { get; set; }

    public Identity.Contracts.TenantRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public long Version { get; set; } = 1;
}

internal sealed class InvitationEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public required string NormalizedEmail { get; set; }

    public Identity.Contracts.TenantRole Role { get; set; }

    public Guid? CampId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsPlatformInvitation { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public Guid? RotatedFromId { get; set; }

    public long Version { get; set; } = 1;
}

internal sealed class TransferableInvitationEntity
{
    public Guid Id { get; set; }

    public Guid CreatedByUserId { get; set; }

    public required string TokenHash { get; set; }

    public bool IsSuperAdmin { get; set; }

    public Guid? OrganizationId { get; set; }

    public Identity.Contracts.OrganizationRole? OrganizationRole { get; set; }

    public Guid? CampId { get; set; }

    public Identity.Contracts.CampRole? CampRole { get; set; }

    public string? NewOrganizationName { get; set; }

    public string? NewOrganizationSlug { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ReservedUntil { get; set; }

    public Guid? ReservedByUserId { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? RotatedFromId { get; set; }

    public long Version { get; set; } = 1;
}

internal sealed class InvitationRegistrationEntity
{
    public Guid Id { get; set; }

    public Guid InvitationId { get; set; }

    public Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

internal sealed class LoginChallengeEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string NormalizedEmail { get; set; }

    public required string CodeHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int FailedAttempts { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

internal sealed class EmailChangeChallengeEntity
{
    public Guid UserId { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public required string CodeHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int FailedAttempts { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

internal sealed class LoginSessionEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public required string IpAddress { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public required string RefreshTokenHash { get; set; }

    public bool RememberMe { get; set; }

    public DateTimeOffset ReauthenticatedAt { get; set; }

    public long Version { get; set; } = 1;
}

internal sealed class LoginRateEventEntity
{
    public long Id { get; set; }

    public required string Partition { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class PasswordResetTokenEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

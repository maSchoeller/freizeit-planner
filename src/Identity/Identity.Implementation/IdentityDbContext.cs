using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public DateTimeOffset? DeletionScheduledAt { get; set; }
}

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    internal DbSet<LoginChallengeEntity> LoginChallenges => Set<LoginChallengeEntity>();

    internal DbSet<LoginSessionEntity> LoginSessions => Set<LoginSessionEntity>();

    internal DbSet<LoginRateEventEntity> LoginRateEvents => Set<LoginRateEventEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        builder.Entity<ApplicationUser>().ToTable("users");
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
        });
        builder.Entity<LoginRateEventEntity>(entity =>
        {
            entity.ToTable("login_rate_events");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Partition, item.OccurredAt });
            entity.Property(item => item.Partition).HasMaxLength(400);
        });
    }
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

internal sealed class LoginSessionEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public required string IpAddress { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}

internal sealed class LoginRateEventEntity
{
    public long Id { get; set; }

    public required string Partition { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

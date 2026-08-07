using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class EfIdentityLifecycleState(IdentityDbContext dbContext) : IIdentityLifecycleState
{
    public async ValueTask<LifecycleUser?> FindUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(item => item.Id == userId)
            .Select(item => new LifecycleUser(
                item.Id,
                item.Email!,
                item.NormalizedEmail!,
                item.DisplayName,
                item.IsPlatformAdmin,
                item.DeletionScheduledAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<LifecycleUser?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(item => item.NormalizedEmail == normalizedEmail)
            .Select(item => new LifecycleUser(
                item.Id,
                item.Email!,
                item.NormalizedEmail!,
                item.DisplayName,
                item.IsPlatformAdmin,
                item.DeletionScheduledAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask SaveUserAsync(LifecycleUser user, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == user.Id, cancellationToken);
        if (entity is null)
        {
            dbContext.Users.Add(ToApplicationUser(user));
        }
        else
        {
            ApplyUser(entity, user);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<OrganizationRecord?> FindOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .Where(item => item.Id == organizationId)
            .Select(item => new OrganizationRecord(
                item.Id,
                item.Name,
                item.Slug,
                item.Status,
                item.DeletionScheduledAt,
                item.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<OrganizationRecord?> FindOrganizationBySlugAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .Where(item => item.Slug == slug)
            .Select(item => new OrganizationRecord(
                item.Id,
                item.Name,
                item.Slug,
                item.Status,
                item.DeletionScheduledAt,
                item.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<MembershipRecord?> FindMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Memberships
            .Where(item => item.OrganizationId == organizationId && item.UserId == userId)
            .Select(item => new MembershipRecord(item.OrganizationId, item.UserId, item.Role, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<MembershipRecord>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Memberships
            .Where(item => item.UserId == userId)
            .Select(item => new MembershipRecord(item.OrganizationId, item.UserId, item.Role, item.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async ValueTask<int> CountActiveOwnersAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Memberships.CountAsync(
            item => item.OrganizationId == organizationId
                && item.IsActive
                && item.Role == Identity.Contracts.TenantRole.Owner,
            cancellationToken);
    }

    public async ValueTask SaveMembershipAsync(
        MembershipRecord membership,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Memberships.SingleOrDefaultAsync(
            item => item.OrganizationId == membership.OrganizationId && item.UserId == membership.UserId,
            cancellationToken);
        if (entity is null)
        {
            dbContext.Memberships.Add(ToMembership(membership));
        }
        else
        {
            entity.Role = membership.Role;
            entity.IsActive = membership.IsActive;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<InvitationRecord?> FindInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Invitations
            .Where(item => item.Id == invitationId)
            .Select(item => new InvitationRecord(
                item.Id,
                item.OrganizationId,
                item.NormalizedEmail,
                item.Role,
                item.CampId,
                item.TokenHash,
                item.CreatedAt,
                item.ExpiresAt,
                item.IsPlatformInvitation,
                item.RevokedAt,
                item.UsedAt,
                item.RotatedFromId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<InvitationRecord>> ListInvitationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Invitations
            .Where(item => item.OrganizationId == organizationId)
            .Select(item => new InvitationRecord(
                item.Id,
                item.OrganizationId,
                item.NormalizedEmail,
                item.Role,
                item.CampId,
                item.TokenHash,
                item.CreatedAt,
                item.ExpiresAt,
                item.IsPlatformInvitation,
                item.RevokedAt,
                item.UsedAt,
                item.RotatedFromId))
            .ToArrayAsync(cancellationToken);
    }

    public async ValueTask SaveOrganizationInvitationAsync(
        OrganizationRecord organization,
        InvitationRecord invitation,
        CancellationToken cancellationToken)
    {
        dbContext.Organizations.Add(ToOrganizationEntity(organization));
        dbContext.Invitations.Add(ToInvitationEntity(invitation));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask SaveInvitationAsync(
        InvitationRecord invitation,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Invitations.SingleOrDefaultAsync(
            item => item.Id == invitation.Id,
            cancellationToken);
        if (entity is null)
        {
            dbContext.Invitations.Add(ToInvitationEntity(invitation));
        }
        else
        {
            entity.RevokedAt = invitation.RevokedAt;
            entity.UsedAt = invitation.UsedAt;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<bool> TryAcceptInvitationAsync(
        InvitationRecord invitation,
        LifecycleUser user,
        MembershipRecord membership,
        CampAssignmentRecord? assignment,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var accepted = await dbContext.Invitations
                .Where(item => item.Id == invitation.Id && item.UsedAt == null && item.RevokedAt == null)
                .ExecuteUpdateAsync(
                    updates => updates.SetProperty(item => item.UsedAt, invitation.UsedAt),
                    cancellationToken);
            if (accepted == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var userEntity = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == user.Id, cancellationToken);
            if (userEntity is null)
            {
                dbContext.Users.Add(ToApplicationUser(user));
            }
            else
            {
                ApplyUser(userEntity, user);
            }

            var membershipEntity = await dbContext.Memberships.SingleOrDefaultAsync(
                item => item.OrganizationId == membership.OrganizationId && item.UserId == membership.UserId,
                cancellationToken);
            if (membershipEntity is null)
            {
                dbContext.Memberships.Add(ToMembership(membership));
            }
            else
            {
                membershipEntity.Role = membership.Role;
                membershipEntity.IsActive = true;
            }

            if (assignment is not null)
            {
                var assignmentEntity = await dbContext.CampAssignments.SingleOrDefaultAsync(
                    item => item.CampId == assignment.CampId && item.UserId == assignment.UserId,
                    cancellationToken);
                if (assignmentEntity is null)
                {
                    dbContext.CampAssignments.Add(new CampAssignmentEntity
                    {
                        OrganizationId = assignment.OrganizationId,
                        CampId = assignment.CampId,
                        UserId = assignment.UserId,
                        Role = assignment.Role
                    });
                }
                else
                {
                    assignmentEntity.Role = assignment.Role;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async ValueTask SaveOrganizationAsync(
        OrganizationRecord organization,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Organizations.SingleAsync(
            item => item.Id == organization.Id,
            cancellationToken);
        entity.Status = organization.Status;
        entity.DeletionScheduledAt = organization.DeletionScheduledAt;
        entity.Version = organization.Version;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask AddInvitationRateEventAsync(
        RateEvent rateEvent,
        CancellationToken cancellationToken)
    {
        dbContext.LoginRateEvents.Add(new LoginRateEventEntity
        {
            Partition = rateEvent.Partition,
            OccurredAt = rateEvent.OccurredAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<int> CountInvitationRateEventsAsync(
        string partition,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        return await dbContext.LoginRateEvents.CountAsync(
            item => item.Partition == partition && item.OccurredAt >= since,
            cancellationToken);
    }

    private static ApplicationUser ToApplicationUser(LifecycleUser user) => new()
    {
        Id = user.Id,
        UserName = user.Email,
        NormalizedUserName = user.NormalizedEmail,
        Email = user.Email,
        NormalizedEmail = user.NormalizedEmail,
        EmailConfirmed = true,
        DisplayName = user.DisplayName,
        IsPlatformAdmin = user.IsPlatformAdmin,
        DeletionScheduledAt = user.DeletionScheduledAt,
        SecurityStamp = Guid.NewGuid().ToString("N")
    };

    private static void ApplyUser(ApplicationUser entity, LifecycleUser user)
    {
        entity.DisplayName = user.DisplayName;
        entity.DeletionScheduledAt = user.DeletionScheduledAt;
    }

    private static OrganizationEntity ToOrganizationEntity(OrganizationRecord organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        Slug = organization.Slug,
        Status = organization.Status,
        DeletionScheduledAt = organization.DeletionScheduledAt,
        Version = organization.Version
    };

    private static MembershipEntity ToMembership(MembershipRecord membership) => new()
    {
        OrganizationId = membership.OrganizationId,
        UserId = membership.UserId,
        Role = membership.Role,
        IsActive = membership.IsActive
    };

    private static InvitationEntity ToInvitationEntity(InvitationRecord invitation) => new()
    {
        Id = invitation.Id,
        OrganizationId = invitation.OrganizationId,
        NormalizedEmail = invitation.NormalizedEmail,
        Role = invitation.Role,
        CampId = invitation.CampId,
        TokenHash = invitation.TokenHash,
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt,
        IsPlatformInvitation = invitation.IsPlatformInvitation,
        RevokedAt = invitation.RevokedAt,
        UsedAt = invitation.UsedAt,
        RotatedFromId = invitation.RotatedFromId
    };
}

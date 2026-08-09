using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class EfIdentityLifecycleState(IdentityDbContext dbContext) :
    IIdentityLifecycleState,
    ITenantAuthorizationState
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
                item.DeletionScheduledAt,
                item.ErasureStartedAt))
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
                item.DeletionScheduledAt,
                item.ErasureStartedAt))
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

    public async ValueTask<IReadOnlyList<OrganizationRecord>> ListOrganizationsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .OrderBy(item => item.Name)
            .Select(item => new OrganizationRecord(
                item.Id,
                item.Name,
                item.Slug,
                item.Status,
                item.DeletionScheduledAt,
                item.Version))
            .ToArrayAsync(cancellationToken);
    }

    public async ValueTask<MembershipRecord?> FindMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Memberships
            .Where(item => item.OrganizationId == organizationId && item.UserId == userId)
            .Select(item => new MembershipRecord(
                item.OrganizationId,
                item.UserId,
                item.Role,
                item.IsActive,
                item.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<MembershipRecord>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Memberships
            .Where(item => item.UserId == userId)
            .Select(item => new MembershipRecord(
                item.OrganizationId,
                item.UserId,
                item.Role,
                item.IsActive,
                item.Version))
            .ToArrayAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<MembershipRecord>> ListOrganizationMembershipsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Memberships
            .Where(item => item.OrganizationId == organizationId)
            .Select(item => new MembershipRecord(
                item.OrganizationId,
                item.UserId,
                item.Role,
                item.IsActive,
                item.Version))
            .ToArrayAsync(cancellationToken);
    }

    public async ValueTask<CampAssignmentRecord?> FindCampAssignmentAsync(
        Guid organizationId,
        Guid campId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.CampAssignments
            .Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.UserId == userId)
            .Select(item => new CampAssignmentRecord(
                item.OrganizationId,
                item.CampId,
                item.UserId,
                item.Role,
                item.IsActive,
                item.Version))
            .SingleOrDefaultAsync(cancellationToken);
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
            dbContext.Entry(entity).Property(item => item.Version).OriginalValue = membership.Version - 1;
            entity.Version = membership.Version;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask SaveCampAssignmentAsync(
        CampAssignmentRecord assignment,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.CampAssignments.SingleOrDefaultAsync(
            item => item.CampId == assignment.CampId && item.UserId == assignment.UserId,
            cancellationToken);
        if (entity is null)
        {
            dbContext.CampAssignments.Add(ToCampAssignment(assignment));
        }
        else
        {
            entity.Role = assignment.Role;
            entity.IsActive = assignment.IsActive;
            dbContext.Entry(entity).Property(item => item.Version).OriginalValue = assignment.Version - 1;
            entity.Version = assignment.Version;
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
                item.RotatedFromId,
                item.Version))
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
                item.RotatedFromId,
                item.Version))
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
            dbContext.Entry(entity).Property(item => item.Version).OriginalValue = invitation.Version - 1;
            entity.Version = invitation.Version;
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
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await TryAcceptInvitationCoreAsync(
                invitation,
                user,
                membership,
                assignment,
                cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var accepted = await TryAcceptInvitationCoreAsync(
            invitation,
            user,
            membership,
            assignment,
            cancellationToken);
        if (accepted)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return accepted;
    }

    private async Task<bool> TryAcceptInvitationCoreAsync(
        InvitationRecord invitation,
        LifecycleUser user,
        MembershipRecord membership,
        CampAssignmentRecord? assignment,
        CancellationToken cancellationToken)
    {
        var accepted = await dbContext.Invitations
            .Where(item => item.Id == invitation.Id && item.UsedAt == null && item.RevokedAt == null)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(item => item.UsedAt, invitation.UsedAt)
                    .SetProperty(item => item.Version, invitation.Version),
                cancellationToken);
        if (accepted == 0)
        {
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
                dbContext.CampAssignments.Add(ToCampAssignment(assignment));
            }
            else
            {
                assignmentEntity.Role = assignment.Role;
                assignmentEntity.IsActive = true;
                assignmentEntity.Version++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
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
        ErasureStartedAt = user.ErasureStartedAt,
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
        IsActive = membership.IsActive,
        Version = membership.Version
    };

    private static CampAssignmentEntity ToCampAssignment(CampAssignmentRecord assignment) => new()
    {
        OrganizationId = assignment.OrganizationId,
        CampId = assignment.CampId,
        UserId = assignment.UserId,
        Role = assignment.Role,
        IsActive = assignment.IsActive,
        Version = assignment.Version
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
        RotatedFromId = invitation.RotatedFromId,
        Version = invitation.Version
    };
}

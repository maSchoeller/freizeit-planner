using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public sealed class UserAdministrationService(IdentityDbContext database) : IUserAdministration
{
    private const int MaximumPageSize = 100;

    public async Task<AdministrationPage<UserAdministrationView>> SearchUsersAsync(
        UserAdministrationQuery query,
        CancellationToken cancellationToken)
    {
        await RequireManagerAsync(query.ActorId, query.OrganizationId, cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize);
        var users = database.Users.AsNoTracking().Where(item => item.ErasureStartedAt == null);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var normalizedSearch = search.ToUpperInvariant();
            var pattern = $"%{search}%";
            var normalizedPattern = $"%{normalizedSearch}%";
            users = users.Where(item =>
                (item.NormalizedEmail != null && EF.Functions.Like(item.NormalizedEmail, normalizedPattern))
                || EF.Functions.Like(item.DisplayName, pattern));
        }
        if (query.OrganizationId is { } organizationId)
        {
            users = users.Where(item => database.Memberships.Any(membership =>
                membership.OrganizationId == organizationId
                && membership.UserId == item.Id
                && membership.Status != MembershipStatus.Removed));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var ids = await users
            .OrderBy(item => item.LastName)
            .ThenBy(item => item.FirstName)
            .ThenBy(item => item.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var items = new List<UserAdministrationView>(ids.Length);
        foreach (var id in ids)
        {
            items.Add(await GetUserAsync(id, cancellationToken));
        }
        return new AdministrationPage<UserAdministrationView>(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<SuperAdminOrganizationView>> ListOrganizationsAsync(
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await RequireSuperAdminAsync(actorId, cancellationToken);
        return await database.Organizations
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new SuperAdminOrganizationView(item.Id, item.Name, item.Slug, item.Status, item.Version))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<UserAdministrationView> ChangeGlobalAccountStatusAsync(
        ChangeGlobalAccountStatusCommand command,
        CancellationToken cancellationToken)
    {
        await RequireSuperAdminAsync(command.ActorId, cancellationToken);
        var user = await RequireUserAsync(command.UserId, cancellationToken);
        EnsureVersion(user.Version, command.ExpectedVersion, "Das Konto wurde zwischenzeitlich geändert.");
        if (command.ActorId == command.UserId && command.Status == AccountStatus.Suspended)
        {
            throw Rule("self_suspension", "Das eigene Konto kann nicht gesperrt werden.");
        }
        if (user.IsSuperAdmin && command.Status == AccountStatus.Suspended)
        {
            await EnsureAnotherActiveSuperAdminAsync(user.Id, cancellationToken);
        }
        user.AccountStatus = command.Status;
        user.Version++;
        if (command.Status == AccountStatus.Suspended)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await database.LoginSessions.Where(item => item.UserId == user.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }
        await database.SaveChangesAsync(cancellationToken);
        return await GetUserAsync(user.Id, cancellationToken);
    }

    public async Task<UserAdministrationView> ChangeSuperAdminAsync(
        ChangeSuperAdminCommand command,
        CancellationToken cancellationToken)
    {
        await RequireSuperAdminAsync(command.ActorId, cancellationToken);
        var user = await RequireUserAsync(command.UserId, cancellationToken);
        EnsureVersion(user.Version, command.ExpectedVersion, "Das Konto wurde zwischenzeitlich geändert.");
        if (user.IsSuperAdmin && !command.IsSuperAdmin)
        {
            await EnsureAnotherActiveSuperAdminAsync(user.Id, cancellationToken);
        }
        user.IsSuperAdmin = command.IsSuperAdmin;
        user.Version++;
        await database.SaveChangesAsync(cancellationToken);
        return await GetUserAsync(user.Id, cancellationToken);
    }

    public async Task<UserAdministrationView> ClearLoginLockoutAsync(
        ClearLoginLockoutCommand command,
        CancellationToken cancellationToken)
    {
        await RequireSuperAdminAsync(command.ActorId, cancellationToken);
        var user = await RequireUserAsync(command.UserId, cancellationToken);
        EnsureVersion(user.Version, command.ExpectedVersion, "Das Konto wurde zwischenzeitlich geändert.");
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.Version++;
        await database.SaveChangesAsync(cancellationToken);
        return await GetUserAsync(user.Id, cancellationToken);
    }

    public async Task<OrganizationAdministrationView> ChangeMembershipAsync(
        ChangeMembershipCommand command,
        CancellationToken cancellationToken)
    {
        await RequireManagerAsync(command.ActorId, command.OrganizationId, cancellationToken);
        _ = await RequireUserAsync(command.UserId, cancellationToken);
        var organization = await database.Organizations.SingleOrDefaultAsync(
            item => item.Id == command.OrganizationId,
            cancellationToken) ?? throw Rule("organization_not_found", "Die Organization wurde nicht gefunden.");
        var membership = await database.Memberships.SingleOrDefaultAsync(
            item => item.OrganizationId == command.OrganizationId && item.UserId == command.UserId,
            cancellationToken);
        if (membership is null)
        {
            if (command.ExpectedVersion != 0)
            {
                throw Rule("version_conflict", "Die Mitgliedschaft wurde zwischenzeitlich geändert.");
            }
            membership = new MembershipEntity
            {
                OrganizationId = command.OrganizationId,
                UserId = command.UserId,
                Version = 1
            };
            database.Memberships.Add(membership);
        }
        else
        {
            EnsureVersion(membership.Version, command.ExpectedVersion, "Die Mitgliedschaft wurde zwischenzeitlich geändert.");
            membership.Version++;
        }
        membership.Status = command.Status;
        membership.OrganizationRole = command.Status == MembershipStatus.Removed ? null : command.Role;
        membership.IsActive = command.Status == MembershipStatus.Active;
        membership.Role = membership.OrganizationRole == OrganizationRole.OrganizationAdmin
            ? TenantRole.OrganizationAdmin
            : TenantRole.Viewer;
        await database.SaveChangesAsync(cancellationToken);
        return await ToOrganizationViewAsync(organization, membership, cancellationToken);
    }

    public async Task<CampAdministrationView?> ChangeCampAssignmentAsync(
        ChangeCampAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        await RequireManagerAsync(command.ActorId, command.OrganizationId, cancellationToken);
        var membership = await database.Memberships.SingleOrDefaultAsync(
            item => item.OrganizationId == command.OrganizationId && item.UserId == command.UserId,
            cancellationToken);
        if (membership is not { Status: MembershipStatus.Active })
        {
            throw Rule("membership_required", "Es besteht keine aktive Organization-Mitgliedschaft.");
        }
        var assignment = await database.CampAssignments.SingleOrDefaultAsync(
            item => item.OrganizationId == command.OrganizationId
                && item.CampId == command.CampId
                && item.UserId == command.UserId,
            cancellationToken);
        if (command.Role is null)
        {
            if (assignment is null)
            {
                return null;
            }
            EnsureVersion(assignment.Version, command.ExpectedVersion, "Die Camp-Zuweisung wurde zwischenzeitlich geändert.");
            database.CampAssignments.Remove(assignment);
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }
        if (assignment is null)
        {
            if (command.ExpectedVersion != 0)
            {
                throw Rule("version_conflict", "Die Camp-Zuweisung wurde zwischenzeitlich geändert.");
            }
            assignment = new CampAssignmentEntity
            {
                OrganizationId = command.OrganizationId,
                CampId = command.CampId,
                UserId = command.UserId,
                Version = 1
            };
            database.CampAssignments.Add(assignment);
        }
        else
        {
            EnsureVersion(assignment.Version, command.ExpectedVersion, "Die Camp-Zuweisung wurde zwischenzeitlich geändert.");
            assignment.Version++;
        }
        assignment.CampRole = command.Role.Value;
        assignment.Role = ToLegacyRole(command.Role.Value);
        assignment.IsActive = true;
        await database.SaveChangesAsync(cancellationToken);
        return new CampAdministrationView(command.CampId, command.CampId.ToString(), command.Role.Value, assignment.Version);
    }

    private async Task RequireManagerAsync(Guid actorId, Guid? organizationId, CancellationToken cancellationToken)
    {
        var actor = await RequireUserAsync(actorId, cancellationToken);
        if (actor.AccountStatus != AccountStatus.Active)
        {
            throw Rule("account_suspended", "Das Konto ist global gesperrt.");
        }
        if (actor.IsSuperAdmin)
        {
            return;
        }
        if (organizationId is not { } requiredOrganizationId)
        {
            throw Rule("super_admin_required", "Diese Aktion erfordert Superadmin-Rechte.");
        }
        var membership = await database.Memberships.AsNoTracking().SingleOrDefaultAsync(
            item => item.OrganizationId == requiredOrganizationId && item.UserId == actorId,
            cancellationToken);
        if (membership is not
            {
                Status: MembershipStatus.Active,
                OrganizationRole: OrganizationRole.OrganizationAdmin
            })
        {
            throw Rule("organization_admin_required", "Diese Aktion erfordert Orgadmin-Rechte.");
        }
    }

    private async Task RequireSuperAdminAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var actor = await RequireUserAsync(actorId, cancellationToken);
        if (!actor.IsSuperAdmin || actor.AccountStatus != AccountStatus.Active)
        {
            throw Rule("super_admin_required", "Diese Aktion erfordert Superadmin-Rechte.");
        }
    }

    private async Task<ApplicationUser> RequireUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
        ?? throw Rule("user_not_found", "Das Konto wurde nicht gefunden.");

    private async Task EnsureAnotherActiveSuperAdminAsync(Guid excludedUserId, CancellationToken cancellationToken)
    {
        if (!await database.Users.AnyAsync(item => item.Id != excludedUserId
            && item.IsSuperAdmin
            && item.AccountStatus == AccountStatus.Active
            && item.ErasureStartedAt == null, cancellationToken))
        {
            throw Rule("last_super_admin", "Der letzte aktive Superadmin kann nicht gesperrt oder herabgestuft werden.");
        }
    }

    private async Task<UserAdministrationView> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleAsync(item => item.Id == userId, cancellationToken);
        var memberships = await database.Memberships.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.OrganizationId)
            .ToArrayAsync(cancellationToken);
        var organizations = new List<OrganizationAdministrationView>(memberships.Length);
        foreach (var membership in memberships)
        {
            var organization = await database.Organizations.AsNoTracking().SingleAsync(
                item => item.Id == membership.OrganizationId,
                cancellationToken);
            organizations.Add(await ToOrganizationViewAsync(organization, membership, cancellationToken));
        }
        return new UserAdministrationView(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.AccountStatus,
            user.IsSuperAdmin,
            user.LockoutEnd,
            organizations,
            user.Version);
    }

    private async Task<OrganizationAdministrationView> ToOrganizationViewAsync(
        OrganizationEntity organization,
        MembershipEntity membership,
        CancellationToken cancellationToken)
    {
        var camps = await database.CampAssignments.AsNoTracking()
            .Where(item => item.OrganizationId == organization.Id
                && item.UserId == membership.UserId
                && item.IsActive)
            .OrderBy(item => item.CampId)
            .Select(item => new CampAdministrationView(
                item.CampId,
                item.CampId.ToString(),
                item.CampRole,
                item.Version))
            .ToArrayAsync(cancellationToken);
        return new OrganizationAdministrationView(
            organization.Id,
            organization.Name,
            organization.Slug,
            membership.Status,
            membership.OrganizationRole,
            camps,
            membership.Version);
    }

    private static TenantRole ToLegacyRole(CampRole role) => role switch
    {
        CampRole.CampLead => TenantRole.CampLead,
        CampRole.Member => TenantRole.Member,
        _ => TenantRole.Viewer
    };

    private static void EnsureVersion(long actual, long expected, string message)
    {
        if (actual != expected)
        {
            throw Rule("version_conflict", message);
        }
    }

    private static IdentityRuleException Rule(string code, string message) => new(code, message);
}

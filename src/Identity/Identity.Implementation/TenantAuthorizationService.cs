using Identity.Contracts;

namespace Identity.Implementation;

public sealed class TenantAuthorizationService(ITenantAuthorizationState state) :
    ITenantAccessControl,
    ITenantAdministration,
    ICampMemberDirectory,
    ISuperAdminOrganizationAdministration
{
    public async Task<IReadOnlyList<SuperAdminOrganizationView>> ListOrganizationsAsync(
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await RequireSuperAdminAsync(actorId, cancellationToken);
        return (await state.ListOrganizationsAsync(cancellationToken))
            .Select(item => new SuperAdminOrganizationView(
                item.Id,
                item.Name,
                item.Slug,
                item.Status,
                item.Version))
            .ToArray();
    }

    public async Task<IReadOnlyList<OrganizationMemberView>> ListOrganizationMembersAsync(
        Guid actorId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _ = await RequireManagerAsync(actorId, organizationId, cancellationToken);
        var result = new List<OrganizationMemberView>();
        foreach (var membership in await state.ListOrganizationMembershipsAsync(
                     organizationId,
                     cancellationToken))
        {
            var user = await state.FindUserAsync(membership.UserId, cancellationToken);
            if (user is not null)
            {
                result.Add(new OrganizationMemberView(
                    membership.UserId,
                    membership.Role,
                    membership.IsActive,
                    membership.Version,
                    user.Email,
                    user.DisplayName));
            }
        }
        return result.OrderBy(item => item.DisplayName).ToArray();
    }

    public async Task<IReadOnlyList<CampMemberSummary>> ListCampMembersAsync(
        CampMemberDirectoryQuery query,
        CancellationToken cancellationToken)
    {
        var decision = await AuthorizeCampAsync(
            new CampAccessRequest(query.ActorId, query.OrganizationId, query.CampId, CampAction.Read),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("camp_access_denied", "Dieses Camp darf nicht gelesen werden.");
        }

        return await state.ListCampMembersAsync(
            query.OrganizationId,
            query.CampId,
            cancellationToken);
    }

    public async Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await state.FindUserAsync(request.ActorId, cancellationToken);
        if (actor is null)
        {
            return TenantAccessDecision.Deny(TenantAccessDenial.ActorUnknown);
        }
        var organization = await state.FindOrganizationAsync(request.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return TenantAccessDecision.Deny(TenantAccessDenial.OrganizationNotFound);
        }
        if (organization.Status != OrganizationStatus.Active)
        {
            return TenantAccessDecision.Deny(TenantAccessDenial.OrganizationSuspended);
        }
        var membership = await state.FindMembershipAsync(
            request.OrganizationId,
            request.ActorId,
            cancellationToken);
        if (membership is not { IsActive: true })
        {
            return TenantAccessDecision.Deny(TenantAccessDenial.MembershipRequired);
        }
        return Allows(membership.Role, request.Action)
            ? TenantAccessDecision.Permit(membership.Role)
            : TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied);
    }

    public async Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken)
    {
        var organizationDecision = await AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(
                request.ActorId,
                request.OrganizationId,
                OrganizationAction.Read),
            cancellationToken);
        if (!organizationDecision.Allowed)
        {
            return organizationDecision;
        }
        if (organizationDecision.EffectiveRole is TenantRole.OrganizationAdmin)
        {
            return TenantAccessDecision.Permit(organizationDecision.EffectiveRole.Value);
        }

        var assignment = await state.FindCampAssignmentAsync(
            request.OrganizationId,
            request.CampId,
            request.ActorId,
            cancellationToken);
        if (assignment is not { IsActive: true })
        {
            return TenantAccessDecision.Deny(TenantAccessDenial.CampAssignmentRequired);
        }
        return Allows(assignment.Role, request.Action)
            ? TenantAccessDecision.Permit(assignment.Role)
            : TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied);
    }

    public async Task<OrganizationMemberView> ChangeOrganizationRoleAsync(
        OrganizationRoleChange change,
        CancellationToken cancellationToken)
    {
        var actor = await RequireManagerAsync(change.ActorId, change.OrganizationId, cancellationToken);
        var target = await RequireMembershipAsync(change.OrganizationId, change.UserId, cancellationToken);
        EnsureMayManageRole(actor.Role, target.Role, change.Role);
        target.ChangeRole(change.Role, change.ExpectedVersion);
        await state.SaveMembershipAsync(target, cancellationToken);
        return ToView(target);
    }

    public async Task RemoveOrganizationMemberAsync(
        OrganizationMemberRemoval removal,
        CancellationToken cancellationToken)
    {
        var actor = await RequireManagerAsync(removal.ActorId, removal.OrganizationId, cancellationToken);
        var target = await RequireMembershipAsync(removal.OrganizationId, removal.UserId, cancellationToken);
        EnsureMayManageRole(actor.Role, target.Role, TenantRole.Viewer);
        target.Remove(removal.ExpectedVersion);
        await state.SaveMembershipAsync(target, cancellationToken);
    }

    public async Task<CampAssignmentView> AssignCampMemberAsync(
        CampMemberAssignment assignment,
        CancellationToken cancellationToken)
    {
        if (assignment.Role is TenantRole.OrganizationAdmin)
        {
            throw Rule("role_scope_invalid", "Orgadmin-Rechte gelten nur für die Organization.");
        }
        _ = await RequireMembershipAsync(
            assignment.OrganizationId,
            assignment.UserId,
            cancellationToken);
        var actor = await RequireMembershipAsync(
            assignment.OrganizationId,
            assignment.ActorId,
            cancellationToken);
        if (actor.Role is not TenantRole.OrganizationAdmin)
        {
            var actorAssignment = await state.FindCampAssignmentAsync(
                assignment.OrganizationId,
                assignment.CampId,
                assignment.ActorId,
                cancellationToken);
            if (actorAssignment is not { IsActive: true, Role: TenantRole.CampLead })
            {
                throw Rule("camp_assignment_required", "Die Camp-Leitung ist diesem Camp nicht zugewiesen.");
            }
            if (assignment.Role == TenantRole.CampLead)
            {
                throw Rule("role_escalation", "Eine Camp-Leitung darf keine weitere Camp-Leitung ernennen.");
            }
        }
        var current = await state.FindCampAssignmentAsync(
            assignment.OrganizationId,
            assignment.CampId,
            assignment.UserId,
            cancellationToken);
        if (current is null)
        {
            if (assignment.ExpectedVersion is not null)
            {
                throw Rule("version_conflict", "Die Camp-Zuweisung wurde zwischenzeitlich geändert.");
            }
            current = new CampAssignmentRecord(
                assignment.OrganizationId,
                assignment.CampId,
                assignment.UserId,
                assignment.Role);
        }
        else
        {
            current.Assign(assignment.Role, assignment.ExpectedVersion ?? 0);
        }
        await state.SaveCampAssignmentAsync(current, cancellationToken);
        return ToView(current);
    }

    public async Task RemoveCampMemberAsync(
        CampMemberRemoval removal,
        CancellationToken cancellationToken)
    {
        var actor = await RequireMembershipAsync(
            removal.OrganizationId,
            removal.ActorId,
            cancellationToken);
        if (actor.Role is not TenantRole.OrganizationAdmin)
        {
            var actorAssignment = await state.FindCampAssignmentAsync(
                removal.OrganizationId,
                removal.CampId,
                removal.ActorId,
                cancellationToken);
            if (actorAssignment is not { IsActive: true, Role: TenantRole.CampLead })
            {
                throw Rule("camp_assignment_required", "Die Camp-Leitung ist diesem Camp nicht zugewiesen.");
            }
        }
        var target = await state.FindCampAssignmentAsync(
            removal.OrganizationId,
            removal.CampId,
            removal.UserId,
            cancellationToken)
            ?? throw Rule("camp_assignment_not_found", "Die Camp-Zuweisung wurde nicht gefunden.");
        if (actor.Role is not TenantRole.OrganizationAdmin
            && target.Role == TenantRole.CampLead)
        {
            throw Rule("role_escalation", "Eine Camp-Leitung darf keine andere Camp-Leitung entfernen.");
        }
        target.Remove(removal.ExpectedVersion);
        await state.SaveCampAssignmentAsync(target, cancellationToken);
    }

    public async Task<OrganizationStatusView> ChangeOrganizationStatusAsync(
        OrganizationStatusChange change,
        CancellationToken cancellationToken)
    {
        await RequireSuperAdminAsync(change.ActorId, cancellationToken);
        var organization = await state.FindOrganizationAsync(change.OrganizationId, cancellationToken)
            ?? throw Rule("organization_not_found", "Die Organization wurde nicht gefunden.");
        if (change.Status == OrganizationStatus.Erasing || organization.Status == OrganizationStatus.Erasing)
        {
            throw Rule("organization_erasure_started", "Die endgültige Löschung kann nicht geändert werden.");
        }
        organization.ChangeStatus(change.Status, change.ExpectedVersion);
        await state.SaveOrganizationAsync(organization, cancellationToken);
        return new OrganizationStatusView(organization.Id, organization.Status, organization.Version);
    }

    private async Task RequireSuperAdminAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var actor = await state.FindUserAsync(actorId, cancellationToken)
            ?? throw Rule("user_not_found", "Das Konto wurde nicht gefunden.");
        if (!actor.IsSuperAdmin)
        {
            throw Rule("super_admin_required", "Nur ein Superadmin darf Organizations verwalten.");
        }
    }

    private async Task<MembershipRecord> RequireManagerAsync(
        Guid actorId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var decision = await AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(actorId, organizationId, OrganizationAction.ManageMembers),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("membership_management_denied", "Diese Rolle darf Mitglieder nicht verwalten.");
        }
        return await RequireMembershipAsync(organizationId, actorId, cancellationToken);
    }

    private async Task<MembershipRecord> RequireMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await state.FindMembershipAsync(organizationId, userId, cancellationToken);
        return membership is { IsActive: true }
            ? membership
            : throw Rule("membership_required", "Es besteht keine aktive Organization-Mitgliedschaft.");
    }

    private static bool Allows(TenantRole role, OrganizationAction action) => role switch
    {
        TenantRole.OrganizationAdmin => true,
        _ => action is OrganizationAction.Read or OrganizationAction.Export
    };

    private static bool Allows(TenantRole role, CampAction action) => role switch
    {
        TenantRole.CampLead => true,
        TenantRole.Member => action is CampAction.Read or CampAction.WriteContent or CampAction.Export,
        TenantRole.Viewer => action is CampAction.Read or CampAction.Export,
        _ => false
    };

    private static void EnsureMayManageRole(TenantRole actor, TenantRole current, TenantRole requested)
    {
        if (actor is TenantRole.OrganizationAdmin)
        {
            return;
        }
        throw Rule("role_escalation", "Diese Rollenänderung würde Rechte unzulässig erweitern.");
    }

    private static OrganizationMemberView ToView(MembershipRecord membership) => new(
        membership.UserId,
        membership.Role,
        membership.IsActive,
        membership.Version);

    private static CampAssignmentView ToView(CampAssignmentRecord assignment) => new(
        assignment.UserId,
        assignment.CampId,
        assignment.Role,
        assignment.IsActive,
        assignment.Version);

    private static IdentityRuleException Rule(string code, string message) => new(code, message);
}

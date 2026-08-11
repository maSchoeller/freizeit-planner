using Identity.Contracts;
using Identity.Implementation;
using Xunit;

namespace Identity.Tests;

public sealed class TenantAuthorizationTests
{
    [Theory]
    [InlineData(TenantRole.OrganizationAdmin, OrganizationAction.ManageSettings, true)]
    [InlineData(TenantRole.OrganizationAdmin, OrganizationAction.ManageCamps, true)]
    [InlineData(TenantRole.CampLead, OrganizationAction.ManageCamps, false)]
    [InlineData(TenantRole.Viewer, OrganizationAction.Read, true)]
    public async Task OrganizationPermissionMatrixIsEnforced(
        TenantRole role,
        OrganizationAction action,
        bool expected)
    {
        var fixture = AuthorizationFixture.Create(role);

        var decision = await fixture.Service.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(fixture.ActorId, fixture.OrganizationId, action),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, decision.Allowed);
    }

    [Theory]
    [InlineData(TenantRole.CampLead, CampAction.ManageCamp, true)]
    [InlineData(TenantRole.Member, CampAction.WriteContent, true)]
    [InlineData(TenantRole.Member, CampAction.ManageCamp, false)]
    [InlineData(TenantRole.Viewer, CampAction.Read, true)]
    [InlineData(TenantRole.Viewer, CampAction.WriteContent, false)]
    public async Task CampPermissionMatrixIsEnforced(
        TenantRole assignmentRole,
        CampAction action,
        bool expected)
    {
        var fixture = AuthorizationFixture.Create(TenantRole.Viewer, assignmentRole);

        var decision = await fixture.Service.AuthorizeCampAsync(
            new CampAccessRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                action),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, decision.Allowed);
    }

    [Fact]
    public async Task SuperAdminNeedsExplicitMembershipAndUnavailableOrganizationCannotBeRead()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.OrganizationAdmin);
        fixture.State.Users[0] = new LifecycleUser(
            fixture.ActorId,
            "platform@example.test",
            "PLATFORM@EXAMPLE.TEST",
            "Superadmin",
            true);
        var explicitMembership = await fixture.Service.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                OrganizationAction.Read),
            TestContext.Current.CancellationToken);

        fixture.State.Memberships[0].Remove(fixture.State.Memberships[0].Version);
        var implicitAccess = await fixture.Service.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                OrganizationAction.Read),
            TestContext.Current.CancellationToken);
        fixture = AuthorizationFixture.Create(TenantRole.OrganizationAdmin);
        fixture.State.Organizations[0].Suspend();
        var suspendedDecision = await fixture.Service.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                OrganizationAction.Read),
            TestContext.Current.CancellationToken);
        fixture.State.Organizations[0].ChangeStatus(
            OrganizationStatus.Erasing,
            fixture.State.Organizations[0].Version);
        var erasingDecision = await fixture.Service.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                OrganizationAction.Read),
            TestContext.Current.CancellationToken);

        Assert.True(explicitMembership.Allowed);
        Assert.False(implicitAccess.Allowed);
        Assert.Equal(TenantAccessDenial.MembershipRequired, implicitAccess.Denial);
        Assert.False(suspendedDecision.Allowed);
        Assert.Equal(TenantAccessDenial.OrganizationSuspended, suspendedDecision.Denial);
        Assert.False(erasingDecision.Allowed);
        Assert.Equal(TenantAccessDenial.OrganizationSuspended, erasingDecision.Denial);
    }

    [Fact]
    public async Task ForeignOrganizationAndCampIdentifiersAreDenied()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.Member, TenantRole.Member);
        var foreignOrganization = await fixture.Service.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(
                fixture.ActorId,
                Guid.NewGuid(),
                OrganizationAction.Read),
            TestContext.Current.CancellationToken);
        var foreignCamp = await fixture.Service.AuthorizeCampAsync(
            new CampAccessRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                Guid.NewGuid(),
                CampAction.Read),
            TestContext.Current.CancellationToken);

        Assert.False(foreignOrganization.Allowed);
        Assert.False(foreignCamp.Allowed);
    }

    [Fact]
    public async Task OrganizationAdminMayManageOtherAdminsAndLeaveNoAdminBehind()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.OrganizationAdmin);
        var cancellationToken = TestContext.Current.CancellationToken;
        var changed = await fixture.Service.ChangeOrganizationRoleAsync(
                new OrganizationRoleChange(
                    fixture.ActorId,
                    fixture.OrganizationId,
                    fixture.ActorId,
                    TenantRole.Member,
                    1),
                cancellationToken);

        var adminId = fixture.AddMember(TenantRole.OrganizationAdmin);
        var targetId = fixture.AddMember(TenantRole.Member);
        var promoted = await fixture.Service.ChangeOrganizationRoleAsync(
                new OrganizationRoleChange(
                    adminId,
                    fixture.OrganizationId,
                    targetId,
                    TenantRole.OrganizationAdmin,
                    1),
                cancellationToken);
        await fixture.Service.RemoveOrganizationMemberAsync(
            new OrganizationMemberRemoval(adminId, fixture.OrganizationId, targetId, promoted.Version),
            cancellationToken);

        Assert.Equal(TenantRole.Member, changed.Role);
        Assert.Equal(TenantRole.OrganizationAdmin, promoted.Role);
        Assert.False(Assert.Single(fixture.State.Memberships, item => item.UserId == targetId).IsActive);
    }

    [Fact]
    public async Task CampLeadCanAssignExistingMemberOnlyWithinOwnCamp()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.Viewer, TenantRole.CampLead);
        var targetId = fixture.AddMember(TenantRole.Member);
        var cancellationToken = TestContext.Current.CancellationToken;

        var assigned = await fixture.Service.AssignCampMemberAsync(
            new CampMemberAssignment(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                targetId,
                TenantRole.Member,
                null),
            cancellationToken);
        var foreignCamp = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.AssignCampMemberAsync(
                new CampMemberAssignment(
                    fixture.ActorId,
                    fixture.OrganizationId,
                    Guid.NewGuid(),
                    targetId,
                    TenantRole.Member,
                    null),
                cancellationToken));

        Assert.Equal(TenantRole.Member, assigned.Role);
        Assert.Equal("camp_assignment_required", foreignCamp.ErrorCode);
    }

    [Fact]
    public async Task ResponsibilityDirectoryReturnsOnlyActiveCampReadersWithoutEmail()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.Member, TenantRole.Member);
        var ownerId = fixture.AddMember(TenantRole.OrganizationAdmin);
        var assignedId = fixture.AddMember(TenantRole.Member);
        fixture.State.Assignments.Add(new CampAssignmentRecord(
            fixture.OrganizationId,
            fixture.CampId,
            assignedId,
            TenantRole.Member));
        _ = fixture.AddMember(TenantRole.Viewer);

        var candidates = await fixture.Service.ListCampMembersAsync(
            new CampMemberDirectoryQuery(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, candidates.Count);
        Assert.Contains(candidates, item => item.UserId == fixture.ActorId);
        Assert.Contains(candidates, item => item.UserId == ownerId);
        Assert.Contains(candidates, item => item.UserId == assignedId);
    }

    [Fact]
    public async Task SuperAdminCanListAndSuspendOrganizationsButTenantUserCannot()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.OrganizationAdmin);
        fixture.State.Users[0] = new LifecycleUser(
            fixture.ActorId,
            "platform@example.test",
            "PLATFORM@EXAMPLE.TEST",
            "Superadmin",
            true);
        var cancellationToken = TestContext.Current.CancellationToken;

        var organizations = await fixture.Service.ListOrganizationsAsync(
            fixture.ActorId,
            cancellationToken);
        var status = await fixture.Service.ChangeOrganizationStatusAsync(
            new OrganizationStatusChange(
                fixture.ActorId,
                fixture.OrganizationId,
                OrganizationStatus.Suspended,
                1),
            cancellationToken);

        fixture.State.Users[0] = new LifecycleUser(
            fixture.ActorId,
            "owner@example.test",
            "OWNER@EXAMPLE.TEST",
            "Orgadmin");
        var denied = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ListOrganizationsAsync(fixture.ActorId, cancellationToken));

        Assert.Single(organizations);
        Assert.Equal(OrganizationStatus.Suspended, status.Status);
        Assert.Equal("super_admin_required", denied.ErrorCode);
    }

    [Fact]
    public async Task AuthorizationDenialsCoverUnknownInactiveAndUnassignedActors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var unknown = AuthorizationFixture.Create(TenantRole.Member);
        unknown.State.Users.Clear();
        var unknownDecision = await unknown.Service.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(unknown.ActorId, unknown.OrganizationId, OrganizationAction.Read),
            cancellationToken);

        var inactive = AuthorizationFixture.Create(TenantRole.Member);
        inactive.State.Memberships[0].Remove(inactive.State.Memberships[0].Version);
        var inactiveDecision = await inactive.Service.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(inactive.ActorId, inactive.OrganizationId, OrganizationAction.Read),
            cancellationToken);

        var unassigned = AuthorizationFixture.Create(TenantRole.Viewer);
        var campDecision = await unassigned.Service.AuthorizeCampAsync(
            new CampAccessRequest(unassigned.ActorId, unassigned.OrganizationId, unassigned.CampId, CampAction.Read),
            cancellationToken);
        var directory = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            unassigned.Service.ListCampMembersAsync(
                new CampMemberDirectoryQuery(unassigned.ActorId, unassigned.OrganizationId, unassigned.CampId),
                cancellationToken));

        Assert.Equal(TenantAccessDenial.ActorUnknown, unknownDecision.Denial);
        Assert.Equal(TenantAccessDenial.MembershipRequired, inactiveDecision.Denial);
        Assert.Equal(TenantAccessDenial.CampAssignmentRequired, campDecision.Denial);
        Assert.Equal("camp_access_denied", directory.ErrorCode);
    }

    [Fact]
    public async Task OwnerCanManageNonLastMembersAndCampAssignments()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.OrganizationAdmin);
        var cancellationToken = TestContext.Current.CancellationToken;
        _ = fixture.AddMember(TenantRole.OrganizationAdmin);
        var memberId = fixture.AddMember(TenantRole.Member);

        var changed = await fixture.Service.ChangeOrganizationRoleAsync(
            new OrganizationRoleChange(fixture.ActorId, fixture.OrganizationId, memberId,
                TenantRole.Viewer, 1), cancellationToken);
        var assigned = await fixture.Service.AssignCampMemberAsync(
            new CampMemberAssignment(fixture.ActorId, fixture.OrganizationId, fixture.CampId,
                memberId, TenantRole.Member, null), cancellationToken);
        var reassigned = await fixture.Service.AssignCampMemberAsync(
            new CampMemberAssignment(fixture.ActorId, fixture.OrganizationId, fixture.CampId,
                memberId, TenantRole.Viewer, assigned.Version), cancellationToken);
        await fixture.Service.RemoveCampMemberAsync(
            new CampMemberRemoval(fixture.ActorId, fixture.OrganizationId, fixture.CampId,
                memberId, reassigned.Version), cancellationToken);
        await fixture.Service.RemoveOrganizationMemberAsync(
            new OrganizationMemberRemoval(fixture.ActorId, fixture.OrganizationId, memberId, changed.Version),
            cancellationToken);

        Assert.False(fixture.State.Memberships.Single(item => item.UserId == memberId).IsActive);
        Assert.False(fixture.State.Assignments.Single(item => item.UserId == memberId).IsActive);
    }

    [Fact]
    public async Task CampAssignmentRulesRejectScopeVersionEscalationAndMissingTargets()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = AuthorizationFixture.Create(TenantRole.OrganizationAdmin);
        var memberId = owner.AddMember(TenantRole.Member);
        var invalidScope = await Assert.ThrowsAsync<IdentityRuleException>(() => owner.Service.AssignCampMemberAsync(
            new CampMemberAssignment(owner.ActorId, owner.OrganizationId, owner.CampId,
                memberId, TenantRole.OrganizationAdmin, null), cancellationToken));
        var version = await Assert.ThrowsAsync<IdentityRuleException>(() => owner.Service.AssignCampMemberAsync(
            new CampMemberAssignment(owner.ActorId, owner.OrganizationId, owner.CampId,
                memberId, TenantRole.Member, 1), cancellationToken));
        var missing = await Assert.ThrowsAsync<IdentityRuleException>(() => owner.Service.RemoveCampMemberAsync(
            new CampMemberRemoval(owner.ActorId, owner.OrganizationId, owner.CampId,
                memberId, 1), cancellationToken));

        var lead = AuthorizationFixture.Create(TenantRole.Member, TenantRole.CampLead);
        var otherLeadId = lead.AddMember(TenantRole.Member);
        lead.State.Assignments.Add(new CampAssignmentRecord(
            lead.OrganizationId, lead.CampId, otherLeadId, TenantRole.CampLead));
        var escalation = await Assert.ThrowsAsync<IdentityRuleException>(() => lead.Service.AssignCampMemberAsync(
            new CampMemberAssignment(lead.ActorId, lead.OrganizationId, lead.CampId,
                otherLeadId, TenantRole.CampLead, 1), cancellationToken));
        var removal = await Assert.ThrowsAsync<IdentityRuleException>(() => lead.Service.RemoveCampMemberAsync(
            new CampMemberRemoval(lead.ActorId, lead.OrganizationId, lead.CampId,
                otherLeadId, 1), cancellationToken));

        Assert.Equal("role_scope_invalid", invalidScope.ErrorCode);
        Assert.Equal("version_conflict", version.ErrorCode);
        Assert.Equal("camp_assignment_not_found", missing.ErrorCode);
        Assert.Equal("role_escalation", escalation.ErrorCode);
        Assert.Equal("role_escalation", removal.ErrorCode);
    }

    [Fact]
    public async Task PlatformStatusChangesRejectMissingAndErasingOrganizations()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.OrganizationAdmin);
        fixture.State.Users[0] = new LifecycleUser(
            fixture.ActorId, "superadmin@example.test", "SUPERADMIN@EXAMPLE.TEST", "Superadmin", true);
        var cancellationToken = TestContext.Current.CancellationToken;
        var missing = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeOrganizationStatusAsync(new OrganizationStatusChange(
                fixture.ActorId, Guid.NewGuid(), OrganizationStatus.Active, 1), cancellationToken));
        fixture.State.Organizations[0].ChangeStatus(OrganizationStatus.Erasing, 1);
        var erasing = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeOrganizationStatusAsync(new OrganizationStatusChange(
                fixture.ActorId, fixture.OrganizationId, OrganizationStatus.Active, 2), cancellationToken));

        Assert.Equal("organization_not_found", missing.ErrorCode);
        Assert.Equal("organization_erasure_started", erasing.ErrorCode);
    }

    [Fact]
    public async Task MemberListingOmitsMembershipWithoutUserRecord()
    {
        var fixture = AuthorizationFixture.Create(TenantRole.OrganizationAdmin);
        fixture.State.Memberships.Add(new MembershipRecord(fixture.OrganizationId, Guid.NewGuid(), TenantRole.Member));

        var members = await fixture.Service.ListOrganizationMembersAsync(
            fixture.ActorId, fixture.OrganizationId, TestContext.Current.CancellationToken);

        Assert.Single(members);
    }

    private sealed record AuthorizationFixture(
        Guid ActorId,
        Guid OrganizationId,
        Guid CampId,
        AuthorizationTestState State,
        TenantAuthorizationService Service)
    {
        public static AuthorizationFixture Create(
            TenantRole organizationRole,
            TenantRole? campRole = null)
        {
            var actorId = Guid.Parse("61000000-0000-0000-0000-000000000001");
            var organizationId = Guid.Parse("62000000-0000-0000-0000-000000000001");
            var campId = Guid.Parse("63000000-0000-0000-0000-000000000001");
            var state = new AuthorizationTestState();
            state.Users.Add(new LifecycleUser(
                actorId,
                "owner@example.test",
                "OWNER@EXAMPLE.TEST",
                "Orgadmin"));
            state.Organizations.Add(new OrganizationRecord(
                organizationId,
                "CVJM Sonnenhöhe",
                "sonnenhoehe"));
            state.Memberships.Add(new MembershipRecord(
                organizationId,
                actorId,
                organizationRole));
            if (campRole is { } role)
            {
                state.Assignments.Add(new CampAssignmentRecord(
                    organizationId,
                    campId,
                    actorId,
                    role));
            }
            return new AuthorizationFixture(
                actorId,
                organizationId,
                campId,
                state,
                new TenantAuthorizationService(state));
        }

        public Guid AddMember(TenantRole role)
        {
            var id = Guid.NewGuid();
            State.Users.Add(new LifecycleUser(
                id,
                $"{id:N}@example.test",
                $"{id:N}@EXAMPLE.TEST",
                "Mitglied"));
            State.Memberships.Add(new MembershipRecord(OrganizationId, id, role));
            return id;
        }
    }

    private sealed class AuthorizationTestState : ITenantAuthorizationState
    {
        public List<LifecycleUser> Users { get; } = [];

        public List<OrganizationRecord> Organizations { get; } = [];

        public List<MembershipRecord> Memberships { get; } = [];

        public List<CampAssignmentRecord> Assignments { get; } = [];

        public ValueTask<LifecycleUser?> FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Users.SingleOrDefault(item => item.Id == userId));

        public ValueTask<OrganizationRecord?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Organizations.SingleOrDefault(item => item.Id == organizationId));

        public ValueTask<IReadOnlyList<OrganizationRecord>> ListOrganizationsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<OrganizationRecord>>(Organizations.ToArray());

        public ValueTask<MembershipRecord?> FindMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Memberships.SingleOrDefault(item =>
                item.OrganizationId == organizationId && item.UserId == userId));

        public ValueTask<CampAssignmentRecord?> FindCampAssignmentAsync(
            Guid organizationId,
            Guid campId,
            Guid userId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Assignments.SingleOrDefault(item =>
                item.OrganizationId == organizationId && item.CampId == campId && item.UserId == userId));

        public ValueTask<IReadOnlyList<MembershipRecord>> ListOrganizationMembershipsAsync(
            Guid organizationId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MembershipRecord>>(
                Memberships.Where(item => item.OrganizationId == organizationId).ToArray());

        public ValueTask<IReadOnlyList<CampMemberSummary>> ListCampMembersAsync(
            Guid organizationId,
            Guid campId,
            CancellationToken cancellationToken)
        {
            var result = Memberships
                .Where(item => item.OrganizationId == organizationId && item.IsActive)
                .Where(item => item.Role is TenantRole.OrganizationAdmin or TenantRole.OrganizationAdmin
                    || Assignments.Any(assignment => assignment.OrganizationId == organizationId
                        && assignment.CampId == campId
                        && assignment.UserId == item.UserId
                        && assignment.IsActive))
                .Join(Users, membership => membership.UserId, user => user.Id,
                    (_, user) => new CampMemberSummary(user.Id, user.DisplayName))
                .OrderBy(item => item.DisplayName)
                .ToArray();
            return ValueTask.FromResult<IReadOnlyList<CampMemberSummary>>(result);
        }

        public ValueTask<int> CountActiveOrganizationAdminsAsync(Guid organizationId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Memberships.Count(item =>
                item.OrganizationId == organizationId && item.IsActive && item.Role == TenantRole.OrganizationAdmin));

        public ValueTask SaveMembershipAsync(MembershipRecord membership, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask SaveCampAssignmentAsync(CampAssignmentRecord assignment, CancellationToken cancellationToken)
        {
            var index = Assignments.FindIndex(item =>
                item.CampId == assignment.CampId && item.UserId == assignment.UserId);
            if (index < 0) Assignments.Add(assignment); else Assignments[index] = assignment;
            return ValueTask.CompletedTask;
        }

        public ValueTask SaveOrganizationAsync(OrganizationRecord organization, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}

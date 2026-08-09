using Identity.Contracts;
using Identity.Implementation;
using Xunit;

namespace Identity.Tests;

public sealed class InvitationLifecycleTests
{
    [Fact]
    public async Task PlatformInvitationCreatesOrganizationAndExpiresAfterFortyEightHours()
    {
        var fixture = LifecycleFixture.Create();

        var invitation = await fixture.Service.CreateOrganizationInvitationAsync(
            new OrganizationInvitationRequest(
                fixture.PlatformAdminId,
                "team@example.test",
                "CVJM Sonnenhöhe",
                "sonnenhoehe",
                "192.0.2.20"),
            TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Clock.GetUtcNow().AddHours(48), invitation.ExpiresAt);
        Assert.Equal(TenantRole.Owner, invitation.Role);
        Assert.DoesNotContain(invitation.Token, fixture.State.StoredInvitationHashes);
    }

    [Fact]
    public async Task InvitationIsSingleUseAndExpiredInvitationIsRejected()
    {
        var fixture = LifecycleFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var invitation = await fixture.CreateOrganizationInvitationAsync(cancellationToken);

        var accepted = await fixture.Service.AcceptInvitationAsync(
            new AcceptInvitationRequest(invitation.Token, "Teammitglied"),
            cancellationToken);
        var reused = await fixture.Service.AcceptInvitationAsync(
            new AcceptInvitationRequest(invitation.Token, "Teammitglied"),
            cancellationToken);
        var expiring = await fixture.CreateOrganizationInvitationAsync(cancellationToken, "zweites-team@example.test", "zweite-org");
        fixture.Clock.Advance(TimeSpan.FromHours(48));
        var expired = await fixture.Service.AcceptInvitationAsync(
            new AcceptInvitationRequest(expiring.Token, "Zweites Team"),
            cancellationToken);

        Assert.Equal(InvitationAcceptanceOutcome.Accepted, accepted.Outcome);
        Assert.True(accepted.RequiresLogin);
        Assert.Equal(InvitationAcceptanceOutcome.Used, reused.Outcome);
        Assert.Equal(InvitationAcceptanceOutcome.Expired, expired.Outcome);
    }

    [Fact]
    public async Task RotationRevokesOldTokenAndTeamInvitationLastsSevenDays()
    {
        var fixture = LifecycleFixture.CreateWithOrganization();
        var cancellationToken = TestContext.Current.CancellationToken;
        var invitation = await fixture.Service.IssueTeamInvitationAsync(
            new TeamInvitationRequest(
                fixture.OwnerId,
                fixture.OrganizationId,
                "viewer@example.test",
                TenantRole.Viewer,
                fixture.CampId,
                "192.0.2.21"),
            cancellationToken);
        var rotated = await fixture.Service.RotateInvitationAsync(
            fixture.OwnerId,
            invitation.Id,
            cancellationToken);

        var oldResult = await fixture.Service.AcceptInvitationAsync(
            new AcceptInvitationRequest(invitation.Token, "Altes Token"),
            cancellationToken);
        var newResult = await fixture.Service.AcceptInvitationAsync(
            new AcceptInvitationRequest(rotated.Token, "Neue Person"),
            cancellationToken);

        Assert.Equal(fixture.Clock.GetUtcNow().AddDays(7), rotated.ExpiresAt);
        Assert.Equal(InvitationAcceptanceOutcome.Revoked, oldResult.Outcome);
        Assert.Equal(InvitationAcceptanceOutcome.Accepted, newResult.Outcome);
        Assert.Contains(fixture.State.Assignments, assignment =>
            assignment.CampId == fixture.CampId && assignment.Role == TenantRole.Viewer);
    }

    [Fact]
    public async Task CampInvitationNeverDowngradesAnExistingOrganizationRole()
    {
        var fixture = LifecycleFixture.CreateWithOrganization();
        var cancellationToken = TestContext.Current.CancellationToken;
        var invitation = await fixture.Service.IssueTeamInvitationAsync(
            new TeamInvitationRequest(
                fixture.OwnerId,
                fixture.OrganizationId,
                "owner@example.test",
                TenantRole.Member,
                fixture.CampId,
                "192.0.2.22"),
            cancellationToken);

        var accepted = await fixture.Service.AcceptInvitationAsync(
            new AcceptInvitationRequest(invitation.Token, "Organization Owner"),
            cancellationToken);

        Assert.Equal(InvitationAcceptanceOutcome.Accepted, accepted.Outcome);
        Assert.Equal(
            TenantRole.Owner,
            (await fixture.State.FindMembershipAsync(
                fixture.OrganizationId,
                fixture.OwnerId,
                cancellationToken))?.Role);
        Assert.Contains(fixture.State.Assignments, assignment =>
            assignment.UserId == fixture.OwnerId && assignment.Role == TenantRole.Member);
    }

    [Fact]
    public async Task LastOwnerCannotLeaveOrDeleteAccountAndDeletionHasThirtyDayGrace()
    {
        var fixture = LifecycleFixture.CreateWithOrganization();
        var cancellationToken = TestContext.Current.CancellationToken;

        var leaveError = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.LeaveOrganizationAsync(
                fixture.OwnerId,
                fixture.OrganizationId,
                cancellationToken));
        var deletionError = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ScheduleAccountDeletionAsync(fixture.OwnerId, cancellationToken));

        fixture.State.Memberships.Add(new MembershipRecord(
            fixture.OrganizationId,
            Guid.NewGuid(),
            TenantRole.Owner));
        var schedule = await fixture.Service.ScheduleAccountDeletionAsync(
            fixture.OwnerId,
            cancellationToken);

        Assert.Equal("last_owner", leaveError.ErrorCode);
        Assert.Equal("last_owner", deletionError.ErrorCode);
        Assert.Equal(schedule.ScheduledAt.AddDays(30), schedule.PurgeAt);
        await fixture.Service.CancelAccountDeletionAsync(fixture.OwnerId, cancellationToken);
        Assert.Null((await fixture.State.FindUserAsync(fixture.OwnerId, cancellationToken))?.DeletionScheduledAt);
    }

    [Fact]
    public async Task OrganizationDeletionRequiresFreshCodeAndExactSlug()
    {
        var fixture = LifecycleFixture.CreateWithOrganization();
        var cancellationToken = TestContext.Current.CancellationToken;
        var staleError = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ScheduleOrganizationDeletionAsync(
                new OrganizationDeletionRequest(
                    fixture.OwnerId,
                    fixture.OrganizationId,
                    "sonnenhoehe",
                    fixture.Clock.GetUtcNow().AddMinutes(-11)),
                cancellationToken));
        var slugError = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ScheduleOrganizationDeletionAsync(
                new OrganizationDeletionRequest(
                    fixture.OwnerId,
                    fixture.OrganizationId,
                    "falscher-slug",
                    fixture.Clock.GetUtcNow()),
                cancellationToken));
        var schedule = await fixture.Service.ScheduleOrganizationDeletionAsync(
            new OrganizationDeletionRequest(
                fixture.OwnerId,
                fixture.OrganizationId,
                "sonnenhoehe",
                fixture.Clock.GetUtcNow()),
            cancellationToken);

        Assert.Equal("fresh_reauthentication_required", staleError.ErrorCode);
        Assert.Equal("slug_confirmation_invalid", slugError.ErrorCode);
        Assert.Equal(schedule.ScheduledAt.AddDays(30), schedule.PurgeAt);
        await fixture.Service.CancelOrganizationDeletionAsync(
            fixture.OwnerId,
            fixture.OrganizationId,
            cancellationToken);
        Assert.Null((await fixture.State.FindOrganizationAsync(
            fixture.OrganizationId,
            cancellationToken))?.DeletionScheduledAt);
    }

    [Fact]
    public async Task ClaimedAccountAndOrganizationErasureCannotBeCancelled()
    {
        var fixture = LifecycleFixture.CreateWithOrganization();
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = Assert.Single(fixture.State.Users, item => item.Id == fixture.OwnerId);
        fixture.State.Users.Remove(user);
        fixture.State.Users.Add(new LifecycleUser(
            user.Id,
            user.Email,
            user.NormalizedEmail,
            user.DisplayName,
            user.IsPlatformAdmin,
            fixture.Clock.GetUtcNow().AddDays(-30),
            fixture.Clock.GetUtcNow()));
        var organization = Assert.Single(
            fixture.State.Organizations,
            item => item.Id == fixture.OrganizationId);
        organization.ChangeStatus(OrganizationStatus.Erasing, organization.Version);

        var accountError = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.CancelAccountDeletionAsync(fixture.OwnerId, cancellationToken));
        var organizationError = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.CancelOrganizationDeletionAsync(
                fixture.OwnerId,
                fixture.OrganizationId,
                cancellationToken));

        Assert.Equal("account_erasure_started", accountError.ErrorCode);
        Assert.Equal("organization_erasure_started", organizationError.ErrorCode);
    }

    [Fact]
    public async Task InvitationRateLimitAppliesPerEmailAndIpWithoutStoringAddresses()
    {
        var fixture = LifecycleFixture.CreateWithOrganization();
        var cancellationToken = TestContext.Current.CancellationToken;
        for (var index = 0; index < 5; index++)
        {
            await fixture.Service.IssueTeamInvitationAsync(
                new TeamInvitationRequest(
                    fixture.OwnerId,
                    fixture.OrganizationId,
                    $"person-{index}@example.test",
                    TenantRole.Viewer,
                    fixture.CampId,
                    "192.0.2.25"),
                cancellationToken);
        }

        var error = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.IssueTeamInvitationAsync(
                new TeamInvitationRequest(
                    fixture.OwnerId,
                    fixture.OrganizationId,
                    "person-6@example.test",
                    TenantRole.Viewer,
                    fixture.CampId,
                    "192.0.2.25"),
                cancellationToken));

        Assert.Equal("invitation_rate_limited", error.ErrorCode);
        Assert.DoesNotContain(fixture.State.RateEvents, item =>
            item.Partition.Contains("example.test", StringComparison.OrdinalIgnoreCase)
            || item.Partition.Contains("192.0.2.25", StringComparison.Ordinal));
    }

    private sealed record LifecycleFixture(
        Guid PlatformAdminId,
        Guid OwnerId,
        Guid OrganizationId,
        Guid CampId,
        LifecycleTestState State,
        ManualLifecycleTimeProvider Clock,
        IdentityLifecycleService Service)
    {
        public static LifecycleFixture Create()
        {
            var platformAdminId = Guid.Parse("30000000-0000-0000-0000-000000000001");
            var state = new LifecycleTestState();
            state.Users.Add(new LifecycleUser(
                platformAdminId,
                "platform@example.test",
                "PLATFORM@EXAMPLE.TEST",
                "Plattform-Administration",
                true));
            var clock = new ManualLifecycleTimeProvider(
                new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero));
            var service = new IdentityLifecycleService(
                state,
                clock,
                Enumerable.Range(33, 32).Select(value => (byte)value).ToArray());
            return new LifecycleFixture(
                platformAdminId,
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                state,
                clock,
                service);
        }

        public static LifecycleFixture CreateWithOrganization()
        {
            var fixture = Create();
            var ownerId = Guid.Parse("30000000-0000-0000-0000-000000000002");
            var organizationId = Guid.Parse("40000000-0000-0000-0000-000000000001");
            var campId = Guid.Parse("50000000-0000-0000-0000-000000000001");
            fixture.State.Users.Add(new LifecycleUser(
                ownerId,
                "owner@example.test",
                "OWNER@EXAMPLE.TEST",
                "Organization Owner"));
            fixture.State.Organizations.Add(new OrganizationRecord(
                organizationId,
                "CVJM Sonnenhöhe",
                "sonnenhoehe"));
            fixture.State.Memberships.Add(new MembershipRecord(
                organizationId,
                ownerId,
                TenantRole.Owner));
            return fixture with
            {
                OwnerId = ownerId,
                OrganizationId = organizationId,
                CampId = campId
            };
        }

        public Task<IssuedInvitation> CreateOrganizationInvitationAsync(
            CancellationToken cancellationToken,
            string email = "team@example.test",
            string slug = "sonnenhoehe")
        {
            return Service.CreateOrganizationInvitationAsync(
                new OrganizationInvitationRequest(
                    PlatformAdminId,
                    email,
                    $"Organization {slug}",
                    slug,
                    "192.0.2.20"),
                cancellationToken);
        }
    }

    private sealed class LifecycleTestState : IIdentityLifecycleState
    {
        public List<LifecycleUser> Users { get; } = [];

        public List<OrganizationRecord> Organizations { get; } = [];

        public List<MembershipRecord> Memberships { get; } = [];

        public List<InvitationRecord> Invitations { get; } = [];

        public List<CampAssignmentRecord> Assignments { get; } = [];

        public List<RateEvent> RateEvents { get; } = [];

        public IEnumerable<string> StoredInvitationHashes => Invitations.Select(item => item.TokenHash);

        public ValueTask<LifecycleUser?> FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Users.SingleOrDefault(item => item.Id == userId));

        public ValueTask<LifecycleUser?> FindUserByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Users.SingleOrDefault(item => item.NormalizedEmail == normalizedEmail));

        public ValueTask SaveUserAsync(LifecycleUser user, CancellationToken cancellationToken)
        {
            Replace(Users, user, item => item.Id == user.Id);
            return ValueTask.CompletedTask;
        }

        public ValueTask<OrganizationRecord?> FindOrganizationAsync(
            Guid organizationId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Organizations.SingleOrDefault(item => item.Id == organizationId));

        public ValueTask<OrganizationRecord?> FindOrganizationBySlugAsync(
            string slug,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Organizations.SingleOrDefault(item => item.Slug == slug));

        public ValueTask<MembershipRecord?> FindMembershipAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Memberships.SingleOrDefault(item =>
                item.OrganizationId == organizationId && item.UserId == userId));

        public ValueTask<IReadOnlyList<MembershipRecord>> ListMembershipsAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MembershipRecord>>(
                Memberships.Where(item => item.UserId == userId).ToArray());

        public ValueTask<int> CountActiveOwnersAsync(
            Guid organizationId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Memberships.Count(item =>
                item.OrganizationId == organizationId
                && item.IsActive
                && item.Role == TenantRole.Owner));

        public ValueTask SaveMembershipAsync(
            MembershipRecord membership,
            CancellationToken cancellationToken)
        {
            Replace(Memberships, membership, item =>
                item.OrganizationId == membership.OrganizationId && item.UserId == membership.UserId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<InvitationRecord?> FindInvitationAsync(
            Guid invitationId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Invitations.SingleOrDefault(item => item.Id == invitationId));

        public ValueTask<IReadOnlyList<InvitationRecord>> ListInvitationsAsync(
            Guid organizationId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<InvitationRecord>>(
                Invitations.Where(item => item.OrganizationId == organizationId).ToArray());

        public ValueTask SaveOrganizationInvitationAsync(
            OrganizationRecord organization,
            InvitationRecord invitation,
            CancellationToken cancellationToken)
        {
            Organizations.Add(organization);
            Invitations.Add(invitation);
            return ValueTask.CompletedTask;
        }

        public ValueTask SaveInvitationAsync(
            InvitationRecord invitation,
            CancellationToken cancellationToken)
        {
            Replace(Invitations, invitation, item => item.Id == invitation.Id);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryAcceptInvitationAsync(
            InvitationRecord invitation,
            LifecycleUser user,
            MembershipRecord membership,
            CampAssignmentRecord? assignment,
            CancellationToken cancellationToken)
        {
            var stored = Invitations.SingleOrDefault(item => item.Id == invitation.Id);
            if (stored is { UsedAt: not null } && !ReferenceEquals(stored, invitation))
            {
                return ValueTask.FromResult(false);
            }
            Replace(Invitations, invitation, item => item.Id == invitation.Id);
            Replace(Users, user, item => item.Id == user.Id);
            Replace(Memberships, membership, item =>
                item.OrganizationId == membership.OrganizationId && item.UserId == membership.UserId);
            if (assignment is not null)
            {
                Assignments.Add(assignment);
            }
            return ValueTask.FromResult(true);
        }

        public ValueTask SaveOrganizationAsync(
            OrganizationRecord organization,
            CancellationToken cancellationToken)
        {
            Replace(Organizations, organization, item => item.Id == organization.Id);
            return ValueTask.CompletedTask;
        }

        public ValueTask AddInvitationRateEventAsync(
            RateEvent rateEvent,
            CancellationToken cancellationToken)
        {
            RateEvents.Add(rateEvent);
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> CountInvitationRateEventsAsync(
            string partition,
            DateTimeOffset since,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RateEvents.Count(item =>
                item.Partition == partition && item.OccurredAt >= since));

        private static void Replace<T>(List<T> items, T value, Func<T, bool> predicate)
        {
            var index = items.FindIndex(item => predicate(item));
            if (index >= 0)
            {
                items[index] = value;
            }
            else
            {
                items.Add(value);
            }
        }
    }

    private sealed class ManualLifecycleTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset current = initial;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}

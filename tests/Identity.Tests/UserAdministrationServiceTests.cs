using Identity.Contracts;
using Identity.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class UserAdministrationServiceTests
{
    [Fact]
    public async Task LastActiveSuperAdminCannotBeSuspendedOrDemoted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);

        var selfSuspension = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeGlobalAccountStatusAsync(
                new ChangeGlobalAccountStatusCommand(
                    fixture.SuperAdminId,
                    fixture.SuperAdminId,
                    AccountStatus.Suspended,
                    1),
                cancellationToken));
        var demotion = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeSuperAdminAsync(
                new ChangeSuperAdminCommand(
                    fixture.SuperAdminId,
                    fixture.SuperAdminId,
                    false,
                    1),
                cancellationToken));

        Assert.Equal("self_suspension", selfSuspension.ErrorCode);
        Assert.Equal("last_super_admin", demotion.ErrorCode);
    }

    [Fact]
    public async Task OrganizationAdminMayRemoveTheLastOrganizationAdmin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var organizationId = Guid.NewGuid();
        fixture.Database.Organizations.Add(new OrganizationEntity
        {
            Id = organizationId,
            Name = "Evangelisches Jugendwerk",
            Slug = "ejw",
            Status = OrganizationStatus.Active
        });
        fixture.Database.Memberships.Add(new MembershipEntity
        {
            OrganizationId = organizationId,
            UserId = fixture.OrganizationAdminId,
            Status = MembershipStatus.Active,
            OrganizationRole = OrganizationRole.OrganizationAdmin,
            Role = TenantRole.OrganizationAdmin
        });
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var changed = await fixture.Service.ChangeMembershipAsync(
            new ChangeMembershipCommand(
                fixture.OrganizationAdminId,
                organizationId,
                fixture.OrganizationAdminId,
                MembershipStatus.Removed,
                null,
                1),
            cancellationToken);

        Assert.Equal(MembershipStatus.Removed, changed.Status);
        Assert.Null(changed.Role);
    }

    [Fact]
    public async Task SuperAdminSearchesPagedUsersAndListsOrganizationAssignments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var organization = await fixture.AddOrganizationAsync("CVJM Sonnenhöhe", "sonnenhoehe", cancellationToken);
        fixture.Database.Memberships.Add(new MembershipEntity
        {
            OrganizationId = organization.Id,
            UserId = fixture.OrganizationAdminId,
            Status = MembershipStatus.Active,
            OrganizationRole = OrganizationRole.OrganizationAdmin,
            Role = TenantRole.OrganizationAdmin
        });
        fixture.Database.CampAssignments.Add(new CampAssignmentEntity
        {
            OrganizationId = organization.Id,
            CampId = Guid.NewGuid(),
            UserId = fixture.OrganizationAdminId,
            CampRole = CampRole.Member,
            Role = TenantRole.Member
        });
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var global = await fixture.Service.SearchUsersAsync(
            new UserAdministrationQuery(fixture.SuperAdminId, "ORGADMIN", 0, 500), cancellationToken);
        var scoped = await fixture.Service.SearchUsersAsync(
            new UserAdministrationQuery(fixture.OrganizationAdminId, "orgadmin", 1, 25, organization.Id),
            cancellationToken);
        var organizations = await fixture.Service.ListOrganizationsAsync(fixture.SuperAdminId, cancellationToken);

        var user = Assert.Single(global.Items);
        Assert.Equal(1, global.Page);
        Assert.Equal(100, global.PageSize);
        Assert.Single(user.Organizations);
        Assert.Single(user.Organizations[0].Camps);
        Assert.Single(scoped.Items);
        Assert.Equal(organization.Id, Assert.Single(organizations).OrganizationId);
    }

    [Fact]
    public async Task SuperAdminSuspendsUnlocksAndChangesGlobalRights()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var secondSuperAdminId = await fixture.AddUserAsync("second@example.test", true, cancellationToken);
        var target = await fixture.Database.Users.SingleAsync(
            item => item.Id == secondSuperAdminId, cancellationToken);
        target.AccessFailedCount = 10;
        target.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
        fixture.Database.LoginSessions.Add(new LoginSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = secondSuperAdminId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            IpAddress = "192.0.2.1",
            RefreshTokenHash = new string('A', 64),
            RememberMe = false,
            ReauthenticatedAt = DateTimeOffset.UtcNow
        });
        await fixture.Database.SaveChangesAsync(cancellationToken);

        var suspended = await fixture.Service.ChangeGlobalAccountStatusAsync(
            new ChangeGlobalAccountStatusCommand(
                fixture.SuperAdminId, secondSuperAdminId, AccountStatus.Suspended, target.Version),
            cancellationToken);
        var restored = await fixture.Service.ChangeGlobalAccountStatusAsync(
            new ChangeGlobalAccountStatusCommand(
                fixture.SuperAdminId, secondSuperAdminId, AccountStatus.Active, suspended.Version),
            cancellationToken);
        var demoted = await fixture.Service.ChangeSuperAdminAsync(
            new ChangeSuperAdminCommand(fixture.SuperAdminId, secondSuperAdminId, false, restored.Version),
            cancellationToken);
        var promoted = await fixture.Service.ChangeSuperAdminAsync(
            new ChangeSuperAdminCommand(fixture.SuperAdminId, secondSuperAdminId, true, demoted.Version),
            cancellationToken);
        var unlocked = await fixture.Service.ClearLoginLockoutAsync(
            new ClearLoginLockoutCommand(fixture.SuperAdminId, secondSuperAdminId, promoted.Version),
            cancellationToken);

        Assert.Equal(AccountStatus.Active, unlocked.AccountStatus);
        Assert.True(unlocked.IsSuperAdmin);
        Assert.Null(unlocked.LoginLockedUntil);
        Assert.Empty(fixture.Database.LoginSessions);
    }

    [Fact]
    public async Task MembershipAndAllCampRolesCanBeCreatedChangedAndRemoved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var organization = await fixture.AddOrganizationAsync("EJW", "ejw", cancellationToken);
        var memberId = await fixture.AddUserAsync("member@example.test", false, cancellationToken);

        var membership = await fixture.Service.ChangeMembershipAsync(
            new ChangeMembershipCommand(
                fixture.SuperAdminId,
                organization.Id,
                memberId,
                MembershipStatus.Active,
                OrganizationRole.OrganizationAdmin,
                0),
            cancellationToken);
        var campId = Guid.NewGuid();
        var lead = await fixture.Service.ChangeCampAssignmentAsync(
            new ChangeCampAssignmentCommand(
                fixture.SuperAdminId, organization.Id, campId, memberId, CampRole.CampLead, 0),
            cancellationToken);
        var member = await fixture.Service.ChangeCampAssignmentAsync(
            new ChangeCampAssignmentCommand(
                fixture.SuperAdminId, organization.Id, campId, memberId, CampRole.Member, lead!.Version),
            cancellationToken);
        var viewer = await fixture.Service.ChangeCampAssignmentAsync(
            new ChangeCampAssignmentCommand(
                fixture.SuperAdminId, organization.Id, campId, memberId, CampRole.Viewer, member!.Version),
            cancellationToken);
        var removed = await fixture.Service.ChangeCampAssignmentAsync(
            new ChangeCampAssignmentCommand(
                fixture.SuperAdminId, organization.Id, campId, memberId, null, viewer!.Version),
            cancellationToken);
        var absent = await fixture.Service.ChangeCampAssignmentAsync(
            new ChangeCampAssignmentCommand(
                fixture.SuperAdminId, organization.Id, campId, memberId, null, 0),
            cancellationToken);
        var suspended = await fixture.Service.ChangeMembershipAsync(
            new ChangeMembershipCommand(
                fixture.SuperAdminId,
                organization.Id,
                memberId,
                MembershipStatus.Suspended,
                null,
                membership.Version),
            cancellationToken);

        Assert.Equal(CampRole.CampLead, lead.Role);
        Assert.Equal(CampRole.Member, member.Role);
        Assert.Equal(CampRole.Viewer, viewer.Role);
        Assert.Null(removed);
        Assert.Null(absent);
        Assert.Equal(MembershipStatus.Suspended, suspended.Status);
        Assert.Null(suspended.Role);
    }

    [Fact]
    public async Task AdministrationRejectsInvalidActorsTargetsMembershipsAndVersions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(cancellationToken);
        var organization = await fixture.AddOrganizationAsync("EJW", "ejw", cancellationToken);
        var memberId = await fixture.AddUserAsync("member@example.test", false, cancellationToken);

        var globalDenied = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.SearchUsersAsync(
                new UserAdministrationQuery(fixture.OrganizationAdminId, null), cancellationToken));
        var organizationDenied = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.SearchUsersAsync(
                new UserAdministrationQuery(fixture.OrganizationAdminId, null, OrganizationId: organization.Id),
                cancellationToken));
        var missingUser = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeSuperAdminAsync(
                new ChangeSuperAdminCommand(fixture.SuperAdminId, Guid.NewGuid(), true, 0), cancellationToken));
        var membershipVersion = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeMembershipAsync(
                new ChangeMembershipCommand(
                    fixture.SuperAdminId,
                    organization.Id,
                    memberId,
                    MembershipStatus.Active,
                    null,
                    2),
                cancellationToken));
        var inactiveCampMembership = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeCampAssignmentAsync(
                new ChangeCampAssignmentCommand(
                    fixture.SuperAdminId, organization.Id, Guid.NewGuid(), memberId, CampRole.Member, 0),
                cancellationToken));
        var missingOrganization = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeMembershipAsync(
                new ChangeMembershipCommand(
                    fixture.SuperAdminId,
                    Guid.NewGuid(),
                    memberId,
                    MembershipStatus.Active,
                    null,
                    0),
                cancellationToken));
        fixture.Database.Memberships.Add(new MembershipEntity
        {
            OrganizationId = organization.Id,
            UserId = memberId,
            Status = MembershipStatus.Active,
            Role = TenantRole.Member
        });
        await fixture.Database.SaveChangesAsync(cancellationToken);
        var campId = Guid.NewGuid();
        var newCampVersion = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeCampAssignmentAsync(
                new ChangeCampAssignmentCommand(
                    fixture.SuperAdminId, organization.Id, campId, memberId, CampRole.Member, 2),
                cancellationToken));
        var assignment = await fixture.Service.ChangeCampAssignmentAsync(
            new ChangeCampAssignmentCommand(
                fixture.SuperAdminId, organization.Id, campId, memberId, CampRole.Member, 0),
            cancellationToken);
        var existingCampVersion = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.ChangeCampAssignmentAsync(
                new ChangeCampAssignmentCommand(
                    fixture.SuperAdminId, organization.Id, campId, memberId, CampRole.Viewer, assignment!.Version + 1),
                cancellationToken));
        var actor = await fixture.Database.Users.SingleAsync(
            item => item.Id == fixture.OrganizationAdminId,
            cancellationToken);
        actor.AccountStatus = AccountStatus.Suspended;
        await fixture.Database.SaveChangesAsync(cancellationToken);
        var suspendedActor = await Assert.ThrowsAsync<IdentityRuleException>(() =>
            fixture.Service.SearchUsersAsync(
                new UserAdministrationQuery(fixture.OrganizationAdminId, null, OrganizationId: organization.Id),
                cancellationToken));

        Assert.Equal("super_admin_required", globalDenied.ErrorCode);
        Assert.Equal("organization_admin_required", organizationDenied.ErrorCode);
        Assert.Equal("user_not_found", missingUser.ErrorCode);
        Assert.Equal("version_conflict", membershipVersion.ErrorCode);
        Assert.Equal("membership_required", inactiveCampMembership.ErrorCode);
        Assert.Equal("organization_not_found", missingOrganization.ErrorCode);
        Assert.Equal("version_conflict", newCampVersion.ErrorCode);
        Assert.Equal("version_conflict", existingCampVersion.ErrorCode);
        Assert.Equal("account_suspended", suspendedActor.ErrorCode);
    }

    private sealed class Fixture(
        SqliteConnection connection,
        IdentityDbContext database,
        UserAdministrationService service,
        Guid superAdminId,
        Guid organizationAdminId) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;

        public IdentityDbContext Database { get; } = database;

        public UserAdministrationService Service { get; } = service;

        public Guid SuperAdminId { get; } = superAdminId;

        public Guid OrganizationAdminId { get; } = organizationAdminId;

        public async Task<Guid> AddUserAsync(
            string email,
            bool isSuperAdmin,
            CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid();
            Database.Users.Add(User(id, email, isSuperAdmin));
            await Database.SaveChangesAsync(cancellationToken);
            return id;
        }

        public async Task<OrganizationEntity> AddOrganizationAsync(
            string name,
            string slug,
            CancellationToken cancellationToken)
        {
            var organization = new OrganizationEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = slug,
                Status = OrganizationStatus.Active
            };
            Database.Organizations.Add(organization);
            await Database.SaveChangesAsync(cancellationToken);
            return organization;
        }

        public static async Task<Fixture> CreateAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            var database = new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>()
                    .UseSqlite(connection)
                    .Options);
            await database.Database.EnsureCreatedAsync(cancellationToken);
            var superAdminId = Guid.NewGuid();
            var organizationAdminId = Guid.NewGuid();
            database.Users.AddRange(
                User(superAdminId, "superadmin@example.test", true),
                User(organizationAdminId, "orgadmin@example.test", false));
            await database.SaveChangesAsync(cancellationToken);
            return new Fixture(
                connection,
                database,
                new UserAdministrationService(database),
                superAdminId,
                organizationAdminId);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static ApplicationUser User(Guid id, string email, bool isSuperAdmin) => new()
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            DisplayName = email,
            FirstName = "Test",
            LastName = "Person",
            IsSuperAdmin = isSuperAdmin,
            AccountStatus = AccountStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
    }
}

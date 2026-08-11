using Identity.Contracts;
using Identity.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class EfIdentityStateTests
{
    [Fact]
    public async Task RelationalAdaptersPersistTenantAndEmailChangeState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new IdentityDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var lifecycle = new EfIdentityLifecycleState(database);
        var emailChange = new EfEmailChangeState(database);
        var now = new DateTimeOffset(2027, 8, 2, 10, 0, 0, TimeSpan.Zero);
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var organizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var campId = Guid.Parse("30000000-0000-0000-0000-000000000001");

        var user = new LifecycleUser(userId, "miriam@example.test", "MIRIAM@EXAMPLE.TEST", "Miriam", true);
        await lifecycle.SaveUserAsync(user, cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(userId, (await lifecycle.FindUserAsync(userId, cancellationToken))?.Id);
        Assert.Equal(userId, (await lifecycle.FindUserByEmailAsync(
            "MIRIAM@EXAMPLE.TEST", cancellationToken))?.Id);
        user.Rename("Miriam", "König", 1);
        await lifecycle.SaveUserAsync(user, cancellationToken);
        database.ChangeTracker.Clear();

        var organization = new OrganizationRecord(organizationId, "CVJM Sonnenhöhe", "sonnenhoehe");
        var invitation = new InvitationRecord(
            Guid.Parse("40000000-0000-0000-0000-000000000001"), organizationId,
            "TEAM@EXAMPLE.TEST", TenantRole.Member, null, "hash-1", now, now.AddDays(7), false);
        await lifecycle.SaveOrganizationInvitationAsync(organization, invitation, cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await lifecycle.ListOrganizationsAsync(cancellationToken));
        Assert.Equal(organizationId, (await lifecycle.FindOrganizationAsync(
            organizationId, cancellationToken))?.Id);
        Assert.Equal(organizationId, (await lifecycle.FindOrganizationBySlugAsync(
            "sonnenhoehe", cancellationToken))?.Id);
        Assert.Equal(invitation.Id, (await lifecycle.FindInvitationAsync(
            invitation.Id, cancellationToken))?.Id);
        Assert.Single(await lifecycle.ListInvitationsAsync(organizationId, cancellationToken));

        var membership = new MembershipRecord(organizationId, userId, TenantRole.OrganizationAdmin);
        await lifecycle.SaveMembershipAsync(membership, cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(1, await lifecycle.CountActiveOrganizationAdminsAsync(organizationId, cancellationToken));
        Assert.Single(await lifecycle.ListMembershipsAsync(userId, cancellationToken));
        Assert.Single(await lifecycle.ListOrganizationMembershipsAsync(organizationId, cancellationToken));
        membership.ChangeRole(TenantRole.OrganizationAdmin, membership.Version);
        await lifecycle.SaveMembershipAsync(membership, cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(TenantRole.OrganizationAdmin, (await lifecycle.FindMembershipAsync(
            organizationId, userId, cancellationToken))?.Role);

        var assignment = new CampAssignmentRecord(organizationId, campId, userId, TenantRole.CampLead);
        await lifecycle.SaveCampAssignmentAsync(assignment, cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(campId, (await lifecycle.FindCampAssignmentAsync(
            organizationId, campId, userId, cancellationToken))?.CampId);
        assignment.Remove(assignment.Version);
        await lifecycle.SaveCampAssignmentAsync(assignment, cancellationToken);
        database.ChangeTracker.Clear();

        invitation.Revoke(now.AddHours(1));
        await lifecycle.SaveInvitationAsync(invitation, cancellationToken);
        organization.ChangeStatus(OrganizationStatus.Suspended, organization.Version);
        await lifecycle.SaveOrganizationAsync(organization, cancellationToken);
        await lifecycle.AddInvitationRateEventAsync(new RateEvent("invite:ip", now), cancellationToken);
        database.ChangeTracker.Clear();

        Assert.Equal(userId, (await emailChange.FindUserAsync(userId, cancellationToken))?.Id);
        Assert.False(await emailChange.EmailExistsAsync("NEU@EXAMPLE.TEST", userId, cancellationToken));
        var challenge = new EmailChangeChallenge(userId, "neu@example.test", "NEU@EXAMPLE.TEST",
            "email-code-hash", now.AddMinutes(10));
        await emailChange.SaveChallengeAsync(challenge, cancellationToken);
        challenge.RecordFailure();
        await emailChange.SaveChallengeAsync(challenge, cancellationToken);
        Assert.Equal(1, (await emailChange.FindChallengeAsync(
            userId, "NEU@EXAMPLE.TEST", cancellationToken))?.FailedAttempts);
        var emailUser = Assert.IsType<EmailChangeUser>(await emailChange.FindUserAsync(userId, cancellationToken));
        emailUser.ChangeEmail("neu@example.test", "NEU@EXAMPLE.TEST");
        challenge.MarkUsed(now.AddMinutes(1));
        await emailChange.SaveUserAndChallengeAsync(emailUser, challenge, cancellationToken);
        await emailChange.AddRateEventAsync(new RateEvent("email:ip", now), cancellationToken);
        Assert.True(await emailChange.EmailExistsAsync("NEU@EXAMPLE.TEST", Guid.NewGuid(), cancellationToken));

        var maintenance = new IdentityMaintenanceService(database, new FixedIdentityTimeProvider(now));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            maintenance.CleanupExpiredAsync(0, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            maintenance.CleanupExpiredAsync(501, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            maintenance.ClaimDueErasuresAsync(0, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            maintenance.ClaimDueErasuresAsync(501, cancellationToken));
        await maintenance.CompleteOrganizationErasureAsync(Guid.NewGuid(), cancellationToken);
        await maintenance.CompleteAccountErasureAsync(Guid.NewGuid(), cancellationToken);
    }

    private sealed class FixedIdentityTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

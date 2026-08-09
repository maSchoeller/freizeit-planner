using Files.Contracts;
using Xunit;

namespace Files.Tests;

public sealed class AttachmentLifecycleTests
{
    [Fact]
    public async Task SoftDeleteCanBeRestoredAndRevokesOutstandingGrants()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        _ = await fixture.Reader.IssueReadGrantAsync(
            new AttachmentReadGrantRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id),
            cancellationToken);

        await fixture.Catalog.MoveToTrashAsync(
            new ChangeAttachmentLifecycle(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id,
                attachment.Version),
            cancellationToken);
        var deleted = fixture.State.Attachments.Single();
        var purgeAt = deleted.PurgeAt;
        var restored = await fixture.Catalog.RestoreAsync(
            new ChangeAttachmentLifecycle(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id,
                deleted.Version),
            cancellationToken);

        Assert.Empty(fixture.State.Grants);
        Assert.Equal(fixture.Clock.GetUtcNow().AddDays(30), purgeAt);
        Assert.Equal(AttachmentLifecycleState.Available, restored.State);
        Assert.Null(restored.PurgeAt);
        Assert.Equal(attachment.Version + 2, restored.Version);
    }

    [Fact]
    public async Task StaleVersionAndExpiredRestoreAreRejected()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        await fixture.Catalog.MoveToTrashAsync(
            new ChangeAttachmentLifecycle(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id,
                attachment.Version),
            cancellationToken);

        var stale = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Catalog.RestoreAsync(
                new ChangeAttachmentLifecycle(
                    fixture.ActorId,
                    fixture.OrganizationId,
                    fixture.CampId,
                    attachment.Id,
                    attachment.Version),
                cancellationToken));
        fixture.Clock.Advance(TimeSpan.FromDays(30));
        var expired = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Catalog.RestoreAsync(
                new ChangeAttachmentLifecycle(
                    fixture.ActorId,
                    fixture.OrganizationId,
                    fixture.CampId,
                    attachment.Id,
                    attachment.Version + 1),
                cancellationToken));

        Assert.Equal("version_conflict", stale.ErrorCode);
        Assert.Equal("attachment_restore_expired", expired.ErrorCode);
    }

    [Fact]
    public async Task PurgeDeletesBlobBeforeMetadataAndReleasesQuota()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        await fixture.Catalog.MoveToTrashAsync(
            new ChangeAttachmentLifecycle(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id,
                attachment.Version),
            cancellationToken);
        fixture.Clock.Advance(TimeSpan.FromDays(30));

        var result = await fixture.Maintenance.PurgeDueAsync(10, cancellationToken);
        var quota = await fixture.Catalog.GetQuotaAsync(
            new AttachmentQuotaQuery(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                AttachmentQuotaScopeType.Camp),
            cancellationToken);

        Assert.Equal(new AttachmentPurgeResult(1, 1, 0), result);
        Assert.Empty(fixture.State.Attachments);
        Assert.Equal(0, fixture.Storage.Count);
        Assert.Equal(0, quota.UsedBytes);
    }

    [Fact]
    public async Task FailedBlobPurgeKeepsMetadataForIdempotentRetry()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        await fixture.Catalog.MoveToTrashAsync(
            new ChangeAttachmentLifecycle(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id,
                attachment.Version),
            cancellationToken);
        fixture.Clock.Advance(TimeSpan.FromDays(30));
        fixture.Storage.FailDeletes = true;

        var failed = await fixture.Maintenance.PurgeDueAsync(10, cancellationToken);
        var retainedAfterFailure = fixture.State.Attachments.Count;
        fixture.Storage.FailDeletes = false;
        var retried = await fixture.Maintenance.PurgeDueAsync(10, cancellationToken);

        Assert.Equal(1, failed.RetryableFailures);
        Assert.Equal(1, retainedAfterFailure);
        Assert.Equal(new AttachmentPurgeResult(1, 1, 0), retried);
        Assert.Empty(fixture.State.Attachments);
    }

    [Fact]
    public async Task CampTrashListsDeletedAttachmentsAcrossOwners()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        await fixture.Catalog.MoveToTrashAsync(
            new ChangeAttachmentLifecycle(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id,
                attachment.Version),
            cancellationToken);

        var trash = await fixture.Catalog.ListTrashAsync(
            new AttachmentTrashQuery(fixture.ActorId, fixture.OrganizationId, fixture.CampId),
            cancellationToken);

        var item = Assert.Single(trash);
        Assert.Equal(attachment.Id, item.Id);
        Assert.Equal(AttachmentLifecycleState.Deleted, item.State);
        Assert.Equal(fixture.Clock.GetUtcNow().AddDays(30), item.PurgeAt);
    }

    [Fact]
    public async Task CampMemberCannotRestoreAnAttachmentThroughTheDirectRouteSeam()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        await fixture.Catalog.MoveToTrashAsync(
            new ChangeAttachmentLifecycle(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id,
                attachment.Version),
            cancellationToken);
        fixture.TenantAccess.DeniedCampActions.Add(Identity.Contracts.CampAction.ManageCamp);

        var exception = await Assert.ThrowsAsync<FilesRuleException>(() => fixture.Catalog.RestoreAsync(
            new ChangeAttachmentLifecycle(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id,
                attachment.Version + 1),
            cancellationToken));

        Assert.Equal("attachment_access_denied", exception.ErrorCode);
    }
}

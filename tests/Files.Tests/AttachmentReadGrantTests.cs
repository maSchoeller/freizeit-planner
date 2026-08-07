using Files.Contracts;
using Xunit;

namespace Files.Tests;

public sealed class AttachmentReadGrantTests
{
    [Fact]
    public async Task GrantIsHashedActorBoundSixtySecondsAndSingleUse()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);

        var grant = await fixture.Reader.IssueReadGrantAsync(
            new AttachmentReadGrantRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id),
            cancellationToken);
        await using var opened = await fixture.Reader.OpenReadAsync(
            new OpenAttachmentReadGrant(fixture.ActorId, grant.Token),
            cancellationToken);
        var reused = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Reader.OpenReadAsync(
                new OpenAttachmentReadGrant(fixture.ActorId, grant.Token),
                cancellationToken));

        Assert.Equal(fixture.Clock.GetUtcNow().AddSeconds(60), grant.ExpiresAt);
        Assert.DoesNotContain(
            grant.Token,
            Convert.ToHexString(fixture.State.Grants.Single().TokenHash),
            StringComparison.Ordinal);
        Assert.Equal(AttachmentContentDisposition.Inline, opened.Disposition);
        Assert.Equal("image/png", opened.ContentType);
        Assert.Equal("attachment_grant_invalid", reused.ErrorCode);
    }

    [Fact]
    public async Task FailedCurrentAuthorizationDoesNotConsumeTheGrant()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        var grant = await fixture.Reader.IssueReadGrantAsync(
            new AttachmentReadGrantRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id),
            cancellationToken);
        fixture.OwnerAuthorization.Denied = true;

        var denied = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Reader.OpenReadAsync(
                new OpenAttachmentReadGrant(fixture.ActorId, grant.Token),
                cancellationToken));
        Assert.Null(fixture.State.Grants.Single().UsedAt);

        fixture.OwnerAuthorization.Denied = false;
        await using var opened = await fixture.Reader.OpenReadAsync(
            new OpenAttachmentReadGrant(fixture.ActorId, grant.Token),
            cancellationToken);

        Assert.Equal("attachment_access_denied", denied.ErrorCode);
        Assert.NotNull(fixture.State.Grants.Single().UsedAt);
        Assert.Equal(attachment.SizeBytes, opened.Length);
    }

    [Fact]
    public async Task WrongActorAndExpiredGrantCannotOpenContent()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        var grant = await fixture.Reader.IssueReadGrantAsync(
            new AttachmentReadGrantRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id),
            cancellationToken);

        var wrongActor = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Reader.OpenReadAsync(
                new OpenAttachmentReadGrant(Guid.NewGuid(), grant.Token),
                cancellationToken));
        Assert.Null(fixture.State.Grants.Single().UsedAt);
        fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        var expired = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Reader.OpenReadAsync(
                new OpenAttachmentReadGrant(fixture.ActorId, grant.Token),
                cancellationToken));

        Assert.Equal("attachment_grant_invalid", wrongActor.ErrorCode);
        Assert.Equal("attachment_grant_invalid", expired.ErrorCode);
    }

    [Fact]
    public async Task ConcurrentRedemptionLetsExactlyOneRequestReadTheBlob()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachment = await fixture.UploadPngAsync(cancellationToken);
        var grant = await fixture.Reader.IssueReadGrantAsync(
            new AttachmentReadGrantRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id),
            cancellationToken);

        var results = await Task.WhenAll(
            TryOpenAsync(fixture, grant.Token),
            TryOpenAsync(fixture, grant.Token));

        Assert.Single(results, item => item);
    }

    [Fact]
    public async Task PdfIsAlwaysDeliveredAsAttachment()
    {
        var fixture = AttachmentFixture.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var bytes = "%PDF-1.7"u8.ToArray();
        await using var source = new MemoryStream(bytes);
        var attachment = await fixture.Catalog.UploadAsync(
            fixture.UploadCommand("ablauf.pdf", "application/pdf", bytes.LongLength),
            source,
            cancellationToken);
        var grant = await fixture.Reader.IssueReadGrantAsync(
            new AttachmentReadGrantRequest(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                attachment.Id),
            cancellationToken);

        await using var opened = await fixture.Reader.OpenReadAsync(
            new OpenAttachmentReadGrant(fixture.ActorId, grant.Token),
            cancellationToken);

        Assert.Equal(AttachmentContentDisposition.Attachment, grant.Disposition);
        Assert.Equal(AttachmentContentDisposition.Attachment, opened.Disposition);
    }

    private static async Task<bool> TryOpenAsync(AttachmentFixture fixture, string token)
    {
        try
        {
            await using var opened = await fixture.Reader.OpenReadAsync(
                new OpenAttachmentReadGrant(fixture.ActorId, token),
                TestContext.Current.CancellationToken);
            return true;
        }
        catch (FilesRuleException exception) when (exception.ErrorCode == "attachment_grant_invalid")
        {
            return false;
        }
    }
}

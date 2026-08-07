using Files.Contracts;
using Xunit;

namespace Files.Tests;

public sealed class AttachmentCatalogTests
{
    [Fact]
    public async Task ValidPngIsStoredPrivatelyWithARandomNameAndReturnedWithoutIt()
    {
        var fixture = AttachmentFixture.Create();
        await using var content = new MemoryStream(
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01, 0x02]);

        var attachment = await fixture.Catalog.UploadAsync(
            new UploadAttachment(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                new AttachmentOwnerReference(AttachmentOwnerType.Note, fixture.OwnerId),
                "lagerplan.png",
                "image/png",
                content.Length),
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentMediaType.Png, attachment.MediaType);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Equal(AttachmentLifecycleState.Available, attachment.State);
        Assert.Equal(content.Length, attachment.SizeBytes);
        Assert.NotEqual("lagerplan.png", fixture.Storage.StoredBlobName);
        Assert.DoesNotContain(fixture.OwnerId.ToString("N"), fixture.Storage.StoredBlobName, StringComparison.OrdinalIgnoreCase);
    }
}

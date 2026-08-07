using Files.Contracts;
using Files.Implementation;
using Xunit;

namespace Files.Tests;

public sealed class AttachmentValidationAndQuotaTests
{
    public static TheoryData<string, string, byte[], AttachmentMediaType> ValidFormats => new()
    {
        { "info.pdf", "application/pdf", "%PDF-1.7"u8.ToArray(), AttachmentMediaType.Pdf },
        { "foto.jpeg", "image/jpeg", [0xff, 0xd8, 0xff, 0x01], AttachmentMediaType.Jpeg },
        { "grafik.png", "image/png", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], AttachmentMediaType.Png },
        { "bild.webp", "image/webp", "RIFF1234WEBP"u8.ToArray(), AttachmentMediaType.WebP }
    };

    [Theory]
    [MemberData(nameof(ValidFormats))]
    public async Task ExactExtensionMimeAndMagicCombinationsAreAccepted(
        string fileName,
        string contentType,
        byte[] bytes,
        AttachmentMediaType expected)
    {
        var fixture = AttachmentFixture.Create();
        await using var content = new MemoryStream(bytes);

        var attachment = await fixture.Catalog.UploadAsync(
            fixture.UploadCommand(fileName, contentType, content.Length),
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, attachment.MediaType);
    }

    public static TheoryData<string, string, byte[]> InvalidFormats => new()
    {
        { "bild.png", "image/jpeg", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a] },
        { "bild.png.exe", "image/png", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a] },
        { "dokument.pdf", "application/pdf", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a] },
        { "vektor.svg", "image/svg+xml", "<svg>"u8.ToArray() },
        { "bild.webp", "image/webp", "RIFF1234NOPE"u8.ToArray() },
        { "bild.png", "image/png", [0x89, 0x50] }
    };

    [Theory]
    [MemberData(nameof(InvalidFormats))]
    public async Task MismatchedOrForbiddenFormatsAreRejected(
        string fileName,
        string contentType,
        byte[] bytes)
    {
        var fixture = AttachmentFixture.Create();
        await using var content = new MemoryStream(bytes);

        var exception = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Catalog.UploadAsync(
                fixture.UploadCommand(fileName, contentType, content.Length),
                content,
                TestContext.Current.CancellationToken));

        Assert.Equal("attachment_format_invalid", exception.ErrorCode);
        Assert.Empty(fixture.State.Attachments);
        Assert.Equal(0, fixture.Storage.Count);
    }

    [Fact]
    public async Task ActualStreamSizeAndDeclaredLengthAreBothEnforced()
    {
        var fixture = AttachmentFixture.Create();
        var oversizedBytes = new byte[(10 * 1024 * 1024) + 1];
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }.CopyTo(oversizedBytes, 0);
        await using var oversized = new MemoryStream(oversizedBytes);
        await using var liedAbout = new MemoryStream(
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

        var tooLarge = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Catalog.UploadAsync(
                fixture.UploadCommand("bild.png", "image/png", null),
                oversized,
                TestContext.Current.CancellationToken));
        var mismatch = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Catalog.UploadAsync(
                fixture.UploadCommand("bild.png", "image/png", liedAbout.Length + 1),
                liedAbout,
                TestContext.Current.CancellationToken));

        Assert.Equal("attachment_too_large", tooLarge.ErrorCode);
        Assert.Equal("attachment_length_mismatch", mismatch.ErrorCode);
    }

    [Fact]
    public async Task OwnerScopeMismatchIsRejectedBeforeAFileIsStored()
    {
        var fixture = AttachmentFixture.Create();
        fixture.OwnerAuthorization.OverrideScope = new AttachmentOwnerScope(
            fixture.OrganizationId,
            Guid.NewGuid(),
            AttachmentQuotaScopeType.Camp);
        await using var content = new MemoryStream(
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

        var exception = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Catalog.UploadAsync(
                fixture.UploadCommand("bild.png", "image/png", content.Length),
                content,
                TestContext.Current.CancellationToken));

        Assert.Equal("attachment_access_denied", exception.ErrorCode);
        Assert.Equal(0, fixture.Storage.Count);
    }

    [Fact]
    public async Task StorageFailureReleasesThePendingQuotaReservation()
    {
        var fixture = AttachmentFixture.Create();
        fixture.Storage.FailWrites = true;
        await using var content = new MemoryStream(
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

        var exception = await Assert.ThrowsAsync<FilesRuleException>(() =>
            fixture.Catalog.UploadAsync(
                fixture.UploadCommand("bild.png", "image/png", content.Length),
                content,
                TestContext.Current.CancellationToken));

        Assert.Equal("attachment_storage_unavailable", exception.ErrorCode);
        Assert.Empty(fixture.State.Attachments);
    }

    [Fact]
    public async Task ConcurrentUploadsCannotOversubscribeTheScopeQuota()
    {
        var fixture = AttachmentFixture.Create();
        fixture.State.Seed(CreateSeed(fixture, 90L * 1024 * 1024));
        var bytes = new byte[6 * 1024 * 1024];
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }.CopyTo(bytes, 0);

        var results = await Task.WhenAll(
            TryUploadAsync(fixture, bytes),
            TryUploadAsync(fixture, bytes));
        var usage = await fixture.Catalog.GetQuotaAsync(
            new AttachmentQuotaQuery(
                fixture.ActorId,
                fixture.OrganizationId,
                fixture.CampId,
                AttachmentQuotaScopeType.Camp),
            TestContext.Current.CancellationToken);

        Assert.Single(results, item => item);
        Assert.Equal(96L * 1024 * 1024, usage.UsedBytes);
        Assert.True(usage.UsedBytes <= usage.LimitBytes);
    }

    private static async Task<bool> TryUploadAsync(AttachmentFixture fixture, byte[] bytes)
    {
        await using var content = new MemoryStream(bytes, writable: false);
        try
        {
            _ = await fixture.Catalog.UploadAsync(
                fixture.UploadCommand("gross.png", "image/png", content.Length),
                content,
                TestContext.Current.CancellationToken);
            return true;
        }
        catch (FilesRuleException exception) when (exception.ErrorCode == "attachment_quota_exceeded")
        {
            return false;
        }
    }

    private static AttachmentRecord CreateSeed(AttachmentFixture fixture, long size) => new(
        Guid.NewGuid(),
        fixture.OrganizationId,
        fixture.CampId,
        new AttachmentOwnerReference(AttachmentOwnerType.Note, fixture.OwnerId),
        AttachmentQuotaScopeType.Camp,
        Guid.NewGuid().ToString("N"),
        "bestand.pdf",
        AttachmentMediaType.Pdf,
        "application/pdf",
        size,
        fixture.ActorId,
        fixture.Clock.GetUtcNow());
}

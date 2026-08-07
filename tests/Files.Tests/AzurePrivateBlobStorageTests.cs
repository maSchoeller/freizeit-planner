using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Files.Implementation;
using Xunit;

namespace Files.Tests;

public sealed class AzurePrivateBlobStorageTests
{
    [Fact]
    [Trait("Category", "Azurite")]
    public async Task AzuriteContainerRemainsPrivateAndSupportsStoreReadDelete()
    {
        var connectionString = Environment.GetEnvironmentVariable("FILES_AZURITE_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var containerName = $"files-test-{Guid.NewGuid():N}";
        var container = new BlobContainerClient(connectionString, containerName);
        var storage = new AzurePrivateBlobStorage(container);
        var blobName = Guid.NewGuid().ToString("N");
        var bytes = "private-content"u8.ToArray();
        try
        {
            await using var source = new MemoryStream(bytes, writable: false);
            await storage.StoreAsync(
                new PrivateBlobWrite(blobName, "image/png", bytes.LongLength),
                source,
                TestContext.Current.CancellationToken);
            var properties = await container.GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken);
            await using var opened = await storage.OpenReadAsync(blobName, TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("Der gespeicherte Test-Blob fehlt.");
            await using var copy = new MemoryStream();
            await opened.Content.CopyToAsync(copy, TestContext.Current.CancellationToken);

            Assert.Equal(PublicAccessType.None, properties.Value.PublicAccess);
            Assert.Equal(bytes, copy.ToArray());
            Assert.True(await storage.DeleteIfExistsAsync(blobName, TestContext.Current.CancellationToken));
            Assert.Null(await storage.OpenReadAsync(blobName, TestContext.Current.CancellationToken));
        }
        finally
        {
            _ = await container.DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
        }
    }
}

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Files.Implementation;

public sealed class AzurePrivateBlobStorage(BlobContainerClient containerClient) : IPrivateBlobStorage
{
    public async Task StoreAsync(
        PrivateBlobWrite write,
        Stream content,
        CancellationToken cancellationToken)
    {
        await EnsurePrivateContainerAsync(cancellationToken);
        var blob = containerClient.GetBlobClient(write.BlobName);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = write.ContentType },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            },
            cancellationToken);
    }

    public async Task<PrivateBlobContent?> OpenReadAsync(
        string blobName,
        CancellationToken cancellationToken)
    {
        await EnsurePrivateContainerAsync(cancellationToken);
        try
        {
            var response = await containerClient
                .GetBlobClient(blobName)
                .DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new PrivateBlobContent(
                response.Value.Content,
                response.Value.Details.ContentLength);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> DeleteIfExistsAsync(
        string blobName,
        CancellationToken cancellationToken)
    {
        await EnsurePrivateContainerAsync(cancellationToken);
        var response = await containerClient.DeleteBlobIfExistsAsync(
            blobName,
            DeleteSnapshotsOption.IncludeSnapshots,
            cancellationToken: cancellationToken);
        return response.Value;
    }

    private async Task EnsurePrivateContainerAsync(CancellationToken cancellationToken)
    {
        _ = await containerClient.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);
        await containerClient.SetAccessPolicyAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);
    }
}

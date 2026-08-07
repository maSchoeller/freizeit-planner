namespace Files.Implementation;

public interface IPrivateBlobStorage
{
    Task StoreAsync(
        PrivateBlobWrite write,
        Stream content,
        CancellationToken cancellationToken);

    Task<PrivateBlobContent?> OpenReadAsync(
        string blobName,
        CancellationToken cancellationToken);

    Task<bool> DeleteIfExistsAsync(
        string blobName,
        CancellationToken cancellationToken);
}

public sealed record PrivateBlobWrite(string BlobName, string ContentType, long Length);

public sealed class PrivateBlobContent(Stream content, long length) : IAsyncDisposable
{
    public Stream Content { get; } = content;

    public long Length { get; } = length;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

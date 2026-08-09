namespace Activity.Contracts;

public interface IActivityJournal
{
    Task<ActivityEvent> RecordAsync(RecordActivity request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityEvent>> ListAsync(ActivityQuery request, CancellationToken cancellationToken);
}

public interface ICampSearchIndex
{
    Task<SearchProjectionResult> UpsertAsync(
        UpsertSearchDocument request,
        CancellationToken cancellationToken);

    Task<SearchProjectionResult> RemoveAsync(
        RemoveSearchDocument request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SearchResult>> SearchAsync(
        CampSearchQuery request,
        CancellationToken cancellationToken);
}

public interface ICampExportFormatter
{
    Task<CsvDocument> FormatAsync(CampCsvRequest request, CancellationToken cancellationToken);
}

public sealed record RecordActivity(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    ActivityKind Kind,
    string ObjectType,
    Guid ObjectId,
    string Title,
    DateTimeOffset Timestamp);

public sealed record ActivityQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    IReadOnlyList<ActivityKind>? Kinds = null,
    IReadOnlyList<string>? ObjectTypes = null,
    Guid? ActorFilter = null,
    DateTimeOffset? Before = null,
    int Limit = 50);

public sealed record ActivityEvent(
    Guid Id,
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    ActivityKind Kind,
    string ObjectType,
    Guid ObjectId,
    string Title,
    DateTimeOffset Timestamp,
    long Version);

public sealed record UpsertSearchDocument(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    string ObjectType,
    Guid ObjectId,
    string Title,
    string SearchText,
    IReadOnlyDictionary<string, string> Metadata,
    long SourceVersion,
    DateTimeOffset Timestamp);

public sealed record RemoveSearchDocument(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    string ObjectType,
    Guid ObjectId,
    long SourceVersion,
    DateTimeOffset Timestamp);

public sealed record SearchProjectionResult(
    string ObjectType,
    Guid ObjectId,
    long SourceVersion,
    long Version,
    bool Applied,
    bool IsRemoved);

public sealed record CampSearchQuery(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    string Query,
    IReadOnlyList<string>? ObjectTypes = null,
    IReadOnlyDictionary<string, string>? MetadataFilters = null,
    int Limit = 50);

public sealed record SearchResult(
    string ObjectType,
    Guid ObjectId,
    string Title,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset UpdatedAt,
    long Version);

public sealed record CampCsvRequest(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    IReadOnlyList<string> GermanHeaders,
    IReadOnlyList<IReadOnlyList<string?>> Rows);

public sealed record CsvDocument(ReadOnlyMemory<byte> Content, string MediaType);

public enum ActivityKind
{
    Created,
    Updated,
    Trashed,
    Restored
}

public sealed class ActivityRuleException(string errorCode, string message) : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

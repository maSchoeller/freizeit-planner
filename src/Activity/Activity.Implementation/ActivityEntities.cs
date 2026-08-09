using Activity.Contracts;

namespace Activity.Implementation;

internal sealed class ActivityEventEntity
{
    public Guid Id { get; set; }

    public Guid ActorId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public ActivityKind Kind { get; set; }

    public required string ObjectType { get; set; }

    public Guid ObjectId { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public long Version { get; set; } = 1;
}

internal sealed class SearchDocumentEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public required string ObjectType { get; set; }

    public Guid ObjectId { get; set; }

    public required string Title { get; set; }

    public required string SearchText { get; set; }

    public required string MetadataJson { get; set; }

    public long SourceVersion { get; set; }

    public bool IsRemoved { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;
}

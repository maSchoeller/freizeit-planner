using Knowledge.Contracts;

namespace Knowledge.Implementation;

internal sealed class NoteEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public required string Title { get; set; }

    public required string Markdown { get; set; }

    public bool IsPinned { get; set; }

    public NoteState State { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid UpdatedBy { get; set; }

    public DateTimeOffset? TrashedAt { get; set; }

    public Guid? TrashedBy { get; set; }

    public DateTimeOffset? PurgeAfter { get; set; }

    public long Version { get; set; } = 1;

    public List<NoteTagEntity> Tags { get; } = [];

    public List<NoteLinkEntity> Links { get; } = [];
}

internal sealed class NoteTagEntity
{
    public Guid Id { get; set; }

    public Guid NoteId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public required string DisplayName { get; set; }

    public required string NormalizedName { get; set; }
}

internal sealed class NoteLinkEntity
{
    public Guid Id { get; set; }

    public Guid NoteId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CampId { get; set; }

    public NoteLinkTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public required string TargetTitleSnapshot { get; set; }
}

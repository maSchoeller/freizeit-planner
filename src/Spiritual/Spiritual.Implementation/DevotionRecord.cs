using Spiritual.Contracts;

namespace Spiritual.Implementation;

public sealed class DevotionRecord
{
    public DevotionRecord(
        Guid id,
        Guid organizationId,
        Guid campId,
        string topic,
        string bibleReference,
        BibleTranslation translation,
        string coreMessage,
        string markdownContent,
        IReadOnlyList<Guid> responsibleUserIds,
        string materialNotes,
        Guid? scheduleEntryId,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        CampId = campId;
        Topic = Required(topic, "topic_required", "Bitte gib ein Thema für die Andacht ein.");
        BibleReference = Required(
            bibleReference,
            "bible_reference_required",
            "Bitte gib eine Bibelstelle ein.");
        Translation = ValidTranslation(translation);
        CoreMessage = Required(
            coreMessage,
            "core_message_required",
            "Bitte gib ein Ziel oder einen Kerngedanken ein.");
        MarkdownContent = Required(
            markdownContent,
            "content_required",
            "Bitte gib einen Inhalt oder eine Gliederung ein.");
        ResponsibleUserIds = Responsibilities(responsibleUserIds);
        MaterialNotes = materialNotes.Trim();
        ScheduleEntryId = scheduleEntryId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public Guid CampId { get; }

    public string Topic { get; private set; }

    public string BibleReference { get; private set; }

    public BibleTranslation Translation { get; private set; }

    public string CoreMessage { get; private set; }

    public string MarkdownContent { get; private set; }

    public IReadOnlyList<Guid> ResponsibleUserIds { get; private set; }

    public string MaterialNotes { get; private set; }

    public Guid? ScheduleEntryId { get; private set; }

    public BibleSnapshot? BibleSnapshot { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public long Version { get; private set; } = 1;

    public void Update(UpdateDevotion command, DateTimeOffset now)
    {
        EnsureVersion(command.ExpectedVersion);
        EnsureActive();
        Topic = Required(command.Topic, "topic_required", "Bitte gib ein Thema für die Andacht ein.");
        BibleReference = Required(
            command.BibleReference,
            "bible_reference_required",
            "Bitte gib eine Bibelstelle ein.");
        Translation = ValidTranslation(command.Translation);
        CoreMessage = Required(
            command.CoreMessage,
            "core_message_required",
            "Bitte gib ein Ziel oder einen Kerngedanken ein.");
        MarkdownContent = Required(
            command.MarkdownContent,
            "content_required",
            "Bitte gib einen Inhalt oder eine Gliederung ein.");
        ResponsibleUserIds = Responsibilities(command.ResponsibleUserIds);
        MaterialNotes = command.MaterialNotes.Trim();
        ScheduleEntryId = command.ScheduleEntryId;
        Touch(now);
    }

    public void ReplaceBibleSnapshot(
        BibleSnapshot snapshot,
        long expectedVersion,
        DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        EnsureActive();
        BibleSnapshot = snapshot;
        Touch(now);
    }

    public void MoveToTrash(long expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        EnsureActive();
        DeletedAt = now;
        Touch(now);
    }

    public void Restore(long expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        if (DeletedAt is null)
        {
            throw Rule("devotion_not_deleted", "Die Andacht befindet sich nicht im Papierkorb.");
        }
        DeletedAt = null;
        Touch(now);
    }

    public void EnsureVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw Rule(
                "version_conflict",
                "Die Andacht wurde zwischenzeitlich geändert. Bitte lade sie neu.");
        }
    }

    public void RestorePersistenceState(
        BibleSnapshot? bibleSnapshot,
        DateTimeOffset updatedAt,
        DateTimeOffset? deletedAt,
        long version)
    {
        BibleSnapshot = bibleSnapshot;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
        Version = version;
    }

    private void EnsureActive()
    {
        if (DeletedAt is not null)
        {
            throw Rule("devotion_deleted", "Die Andacht befindet sich im Papierkorb.");
        }
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }

    private static string Required(string value, string code, string message)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 ? trimmed : throw Rule(code, message);
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<Guid> Responsibilities(
        IReadOnlyList<Guid> values)
    {
        var distinct = values.Where(id => id != Guid.Empty).Distinct().ToArray();
        return distinct.Length > 0
            ? Array.AsReadOnly(distinct)
            : throw Rule(
                "responsibility_required",
                "Bitte ordne der Andacht mindestens eine verantwortliche Person zu.");
    }

    private static BibleTranslation ValidTranslation(BibleTranslation translation) =>
        Enum.IsDefined(translation)
            ? translation
            : throw Rule(
                "translation_not_supported",
                "Diese Bibelübersetzung wird nicht unterstützt.");

    private static SpiritualRuleException Rule(string code, string message) => new(code, message);
}

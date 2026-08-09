using System.Text;
using Activity.Contracts;

internal sealed class PlanningActivityWriter(
    IActivityJournal journal,
    ICampSearchIndex searchIndex,
    TimeProvider timeProvider)
{
    public async Task UpsertAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        ActivityKind kind,
        string objectType,
        Guid objectId,
        string title,
        string searchText,
        IReadOnlyDictionary<string, string> metadata,
        long sourceVersion,
        CancellationToken cancellationToken)
    {
        var timestamp = timeProvider.GetUtcNow();
        var boundedTitle = Bound(title, 160);
        await journal.RecordAsync(new RecordActivity(actorId, organizationId, campId, kind,
            objectType, objectId, boundedTitle, timestamp), cancellationToken);
        await searchIndex.UpsertAsync(new UpsertSearchDocument(actorId, organizationId, campId,
            objectType, objectId, boundedTitle, Bound(searchText, 2000), metadata,
            sourceVersion, timestamp), cancellationToken);
    }

    public async Task RemoveAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        string objectType,
        Guid objectId,
        string title,
        long sourceVersion,
        CancellationToken cancellationToken)
    {
        var timestamp = timeProvider.GetUtcNow();
        await journal.RecordAsync(new RecordActivity(actorId, organizationId, campId, ActivityKind.Trashed,
            objectType, objectId, Bound(title, 160), timestamp), cancellationToken);
        await searchIndex.RemoveAsync(new RemoveSearchDocument(actorId, organizationId, campId,
            objectType, objectId, sourceVersion, timestamp), cancellationToken);
    }

    private static string Bound(string? value, int maxLength)
    {
        var normalized = string.Join(' ', (value ?? string.Empty).Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

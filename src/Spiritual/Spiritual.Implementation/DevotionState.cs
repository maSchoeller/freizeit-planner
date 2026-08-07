namespace Spiritual.Implementation;

public interface IDevotionState
{
    ValueTask<IReadOnlyList<DevotionRecord>> ListAsync(
        Guid organizationId,
        Guid campId,
        bool includeDeleted,
        CancellationToken cancellationToken);

    ValueTask<DevotionRecord?> FindAsync(
        Guid organizationId,
        Guid campId,
        Guid devotionId,
        CancellationToken cancellationToken);

    ValueTask AddAsync(DevotionRecord devotion, CancellationToken cancellationToken);

    ValueTask SaveAsync(DevotionRecord devotion, CancellationToken cancellationToken);
}

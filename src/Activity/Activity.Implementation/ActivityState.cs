using Microsoft.EntityFrameworkCore;

namespace Activity.Implementation;

internal interface IActivityState
{
    void AddEvent(ActivityEventEntity activityEvent);

    Task<IReadOnlyList<ActivityEventEntity>> ListEventsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    Task<SearchDocumentEntity?> FindSearchDocumentAsync(
        Guid organizationId,
        Guid campId,
        string objectType,
        Guid objectId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SearchDocumentEntity>> ListSearchDocumentsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    void AddSearchDocument(SearchDocumentEntity document);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed class EfActivityState(ActivityDbContext dbContext) : IActivityState
{
    public void AddEvent(ActivityEventEntity activityEvent) => dbContext.ActivityEvents.Add(activityEvent);

    public async Task<IReadOnlyList<ActivityEventEntity>> ListEventsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        await dbContext.ActivityEvents
            .Where(item => item.OrganizationId == organizationId && item.CampId == campId)
            .ToListAsync(cancellationToken);

    public Task<SearchDocumentEntity?> FindSearchDocumentAsync(
        Guid organizationId,
        Guid campId,
        string objectType,
        Guid objectId,
        CancellationToken cancellationToken) =>
        dbContext.SearchDocuments.SingleOrDefaultAsync(
            item =>
                item.OrganizationId == organizationId &&
                item.CampId == campId &&
                item.ObjectType == objectType &&
                item.ObjectId == objectId,
            cancellationToken);

    public async Task<IReadOnlyList<SearchDocumentEntity>> ListSearchDocumentsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        await dbContext.SearchDocuments
            .Where(item => item.OrganizationId == organizationId && item.CampId == campId && !item.IsRemoved)
            .ToListAsync(cancellationToken);

    public void AddSearchDocument(SearchDocumentEntity document) => dbContext.SearchDocuments.Add(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

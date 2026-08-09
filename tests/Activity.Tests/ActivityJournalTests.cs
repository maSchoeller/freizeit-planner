using Activity.Contracts;
using Activity.Implementation;
using System.Globalization;
using Identity.Contracts;
using Xunit;

namespace Activity.Tests;

public sealed class ActivityJournalTests
{
    private static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CampId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task RecordedMetadataIsListedWithoutDomainContent()
    {
        var subject = new ActivityService(new TestActivityState(), new AllowActivityAccess());
        var timestamp = DateTimeOffset.Parse("2026-08-07T12:00:00Z", CultureInfo.InvariantCulture);

        var recorded = await subject.RecordAsync(
            new RecordActivity(
                ActorId,
                OrganizationId,
                CampId,
                ActivityKind.Created,
                " Note ",
                Guid.NewGuid(),
                "  Packliste\tFreizeit  ",
                timestamp),
            TestContext.Current.CancellationToken);
        var listed = await subject.ListAsync(
            new ActivityQuery(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken);

        var item = Assert.Single(listed);
        Assert.Equal(recorded, item);
        Assert.Equal("Note", item.ObjectType);
        Assert.Equal("Packliste Freizeit", item.Title);
        Assert.Equal(timestamp, item.Timestamp);
        Assert.Equal(1, item.Version);
    }

    [Fact]
    public async Task RecordRejectsUnboundedTitlesBeforeWriting()
    {
        var state = new TestActivityState();
        var subject = new ActivityService(state, new AllowActivityAccess());

        var exception = await Assert.ThrowsAsync<ActivityRuleException>(() => subject.RecordAsync(
            new RecordActivity(
                ActorId,
                OrganizationId,
                CampId,
                ActivityKind.Updated,
                "Note",
                Guid.NewGuid(),
                new string('x', 161),
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken));

        Assert.Equal("metadata_too_long", exception.ErrorCode);
        Assert.Empty(state.Events);
    }

    [Fact]
    public async Task ListCannotSeeAnotherCampEvenWhenStateContainsItsRows()
    {
        var state = new TestActivityState();
        state.Events.AddRange(
        [
            CreateEvent(CampId, "Eigene Aktivität"),
            CreateEvent(Guid.Parse("30000000-0000-0000-0000-000000000099"), "Fremde Aktivität")
        ]);
        var subject = new ActivityService(state, new AllowActivityAccess());

        var result = await subject.ListAsync(
            new ActivityQuery(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken);

        Assert.Equal("Eigene Aktivität", Assert.Single(result).Title);
    }

    [Fact]
    public async Task DeniedActorCannotReadOrWriteActivity()
    {
        var state = new TestActivityState();
        var subject = new ActivityService(state, new DenyActivityAccess());

        var writeException = await Assert.ThrowsAsync<ActivityRuleException>(() => subject.RecordAsync(
            new RecordActivity(
                ActorId,
                OrganizationId,
                CampId,
                ActivityKind.Created,
                "Note",
                Guid.NewGuid(),
                "Geheim",
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken));
        var readException = await Assert.ThrowsAsync<ActivityRuleException>(() => subject.ListAsync(
            new ActivityQuery(ActorId, OrganizationId, CampId),
            TestContext.Current.CancellationToken));

        Assert.Equal("access_denied", writeException.ErrorCode);
        Assert.Equal("access_denied", readException.ErrorCode);
        Assert.Empty(state.Events);
    }

    private static ActivityEventEntity CreateEvent(Guid campId, string title) => new()
    {
        Id = Guid.NewGuid(),
        ActorId = ActorId,
        OrganizationId = OrganizationId,
        CampId = campId,
        Kind = ActivityKind.Created,
        ObjectType = "Note",
        ObjectId = Guid.NewGuid(),
        Title = title,
        Timestamp = DateTimeOffset.UtcNow
    };
}

internal sealed class TestActivityState : IActivityState
{
    public List<ActivityEventEntity> Events { get; } = [];

    public List<SearchDocumentEntity> SearchDocuments { get; } = [];

    public void AddEvent(ActivityEventEntity activityEvent) => Events.Add(activityEvent);

    public Task<IReadOnlyList<ActivityEventEntity>> ListEventsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ActivityEventEntity>>(Events.Where(item =>
            item.OrganizationId == organizationId && item.CampId == campId).ToList());

    public Task<SearchDocumentEntity?> FindSearchDocumentAsync(
        Guid organizationId,
        Guid campId,
        string objectType,
        Guid objectId,
        CancellationToken cancellationToken) =>
        Task.FromResult(SearchDocuments.SingleOrDefault(item =>
            item.OrganizationId == organizationId &&
            item.CampId == campId &&
            item.ObjectType == objectType &&
            item.ObjectId == objectId));

    public Task<IReadOnlyList<SearchDocumentEntity>> ListSearchDocumentsAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SearchDocumentEntity>>(SearchDocuments.Where(item =>
            item.OrganizationId == organizationId && item.CampId == campId && !item.IsRemoved).ToList());

    public void AddSearchDocument(SearchDocumentEntity document) => SearchDocuments.Add(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class AllowActivityAccess : ITenantAccessControl
{
    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Permit(TenantRole.Member));

    public Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Permit(TenantRole.Member));
}

internal sealed class DenyActivityAccess : ITenantAccessControl
{
    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied));

    public Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied));
}

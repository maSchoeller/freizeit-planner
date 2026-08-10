using System.Reflection;
using System.Security.Claims;
using Camps.Contracts;
using Catering.Contracts;
using Files.Contracts;
using Identity.Contracts;
using Knowledge.Contracts;
using Logistics.Contracts;
using Microsoft.AspNetCore.Http;
using Spiritual.Contracts;
using Xunit;

namespace Api.Tests;

public sealed class WebAdapterTests
{
    private static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CampId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task AttachmentOwnersCoverEverySupportedPlanningAggregate()
    {
        var adapter = CreateAttachmentAdapter(new AccessControl(true), CampStatus.Active);
        var cancellationToken = TestContext.Current.CancellationToken;

        foreach (var ownerType in new[]
                 {
                     AttachmentOwnerType.ScheduleEntry,
                     AttachmentOwnerType.Meal,
                     AttachmentOwnerType.MaterialRequirement,
                     AttachmentOwnerType.Devotion,
                     AttachmentOwnerType.Note
                 })
        {
            var decision = await adapter.AuthorizeAsync(new AttachmentOwnerAccessRequest(
                ActorId, OrganizationId, CampId, new AttachmentOwnerReference(ownerType, TargetId),
                AttachmentOwnerAction.Read), cancellationToken);
            Assert.True(decision.Allowed);
            Assert.Equal(AttachmentQuotaScopeType.Camp, decision.Scope?.QuotaScope);
        }

        var recipe = await adapter.AuthorizeAsync(new AttachmentOwnerAccessRequest(
            ActorId, OrganizationId, null, new AttachmentOwnerReference(AttachmentOwnerType.Recipe, TargetId),
            AttachmentOwnerAction.AddAttachment), cancellationToken);
        Assert.True(recipe.Allowed);
        Assert.Equal(AttachmentQuotaScopeType.OrganizationRecipeLibrary, recipe.Scope?.QuotaScope);
    }

    [Fact]
    public async Task AttachmentAuthorizationDeniesWrongScopeArchiveAndAccessFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var note = new AttachmentOwnerReference(AttachmentOwnerType.Note, TargetId);
        var recipe = new AttachmentOwnerReference(AttachmentOwnerType.Recipe, TargetId);
        var active = CreateAttachmentAdapter(new AccessControl(true), CampStatus.Active);
        var archived = CreateAttachmentAdapter(new AccessControl(true), CampStatus.Archived);
        var denied = CreateAttachmentAdapter(new AccessControl(false), CampStatus.Active);

        Assert.False((await active.AuthorizeAsync(new AttachmentOwnerAccessRequest(
            ActorId, OrganizationId, null, note, AttachmentOwnerAction.Read), cancellationToken)).Allowed);
        Assert.False((await active.AuthorizeAsync(new AttachmentOwnerAccessRequest(
            ActorId, OrganizationId, CampId, recipe, AttachmentOwnerAction.Read), cancellationToken)).Allowed);
        Assert.False((await archived.AuthorizeAsync(new AttachmentOwnerAccessRequest(
            ActorId, OrganizationId, CampId, note, AttachmentOwnerAction.AddAttachment), cancellationToken)).Allowed);
        Assert.False((await denied.AuthorizeAsync(new AttachmentOwnerAccessRequest(
            ActorId, OrganizationId, CampId, note, AttachmentOwnerAction.Read), cancellationToken)).Allowed);
    }

    [Fact]
    public async Task NoteLinksResolveEveryPlanningTargetAndRemoveDuplicates()
    {
        var resolver = new NoteLinkTargetResolver(
            Proxy<ISchedulePlanning>(), Proxy<ICampMealPlanning>(), Proxy<IOrganizationCateringLibrary>(),
            Proxy<IDevotionPlanning>(), Proxy<IMaterialPlanning>(), Proxy<IShoppingPlanning>());
        var links = Enum.GetValues<NoteLinkTargetType>()
            .Select((type, index) => new NoteLinkReference(type, Guid.Parse($"40000000-0000-0000-0000-{index + 1:D12}")))
            .ToList();
        links.Add(links[0]);

        var resolved = await resolver.ResolveAsync(
            new NoteLinkResolutionRequest(ActorId, OrganizationId, CampId, links),
            TestContext.Current.CancellationToken);

        Assert.Equal(Enum.GetValues<NoteLinkTargetType>().Length, resolved.Count);
        Assert.Equal(Enum.GetValues<NoteLinkTargetType>(), resolved.Select(item => item.Type));
    }

    [Fact]
    public async Task KnowledgeAndDevotionContextsReflectArchiveAndReferenceFailures()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var active = new CampDefaults(CampStatus.Active);
        var archived = new CampDefaults(CampStatus.Archived);
        var knowledgeActive = await new KnowledgeCampContextAdapter(active).GetAsync(
            new KnowledgeCampContextRequest(ActorId, OrganizationId, CampId), cancellationToken);
        var knowledgeArchived = await new KnowledgeCampContextAdapter(archived).GetAsync(
            new KnowledgeCampContextRequest(ActorId, OrganizationId, CampId), cancellationToken);
        var devotionActive = new DevotionCampContextAdapter(active, Proxy<IScheduleReferenceAccess>());
        var devotionArchived = new DevotionCampContextAdapter(archived, new RejectScheduleReference());

        Assert.False(knowledgeActive.IsArchived);
        Assert.True(knowledgeArchived.IsArchived);
        Assert.False((await devotionActive.GetAsync(
            new DevotionCampContextRequest(ActorId, OrganizationId, CampId), cancellationToken)).IsArchived);
        Assert.True((await devotionArchived.GetAsync(
            new DevotionCampContextRequest(ActorId, OrganizationId, CampId), cancellationToken)).IsArchived);
        Assert.True(await devotionActive.IsScheduleEntryWritableAsync(
            new DevotionScheduleReference(ActorId, OrganizationId, CampId, TargetId), cancellationToken));
        Assert.False(await devotionArchived.IsScheduleEntryWritableAsync(
            new DevotionScheduleReference(ActorId, OrganizationId, CampId, TargetId), cancellationToken));
    }

    [Theory]
    [InlineData("POST", "/api/v1/invitations/accept", "invitation_acceptance")]
    [InlineData("POST", "/api/v1/invitations/organizations", "platform_create_organization")]
    [InlineData("GET", "/api/v1/platform/organizations", "platform_admin")]
    [InlineData("GET", "/api/v1/organizations/x", "tenant")]
    public void TransactionMiddlewareClassifiesDatabaseOperation(
        string method, string path, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        var operation = InvokePrivate<string>(typeof(TenantDatabaseTransactionMiddleware),
            "ReadOperation", context.Request);

        Assert.Equal(expected, operation);
    }

    [Theory]
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/30000000-0000-0000-0000-000000000001/notes", true, true)]
    [InlineData("/api/v1/organizations/not-a-guid/camps/not-a-guid/notes", false, false)]
    [InlineData("/api/v1/account", false, false)]
    public void TransactionMiddlewareReadsOnlyValidTenantRouteIdentifiers(
        string path, bool hasOrganization, bool hasCamp)
    {
        var result = InvokePrivate<(string OrganizationId, string CampId)>(
            typeof(TenantDatabaseTransactionMiddleware), "ReadTenantRoute", new PathString(path));

        Assert.Equal(hasOrganization, result.OrganizationId.Length > 0);
        Assert.Equal(hasCamp, result.CampId.Length > 0);
    }

    [Theory]
    [InlineData("\"12\"", true, 12)]
    [InlineData("W/\"7\"", true, 7)]
    [InlineData("0", false, 0)]
    [InlineData("invalid", false, 0)]
    public void PlanningVersionsAcceptStrongAndWeakPositiveEtags(
        string header, bool expected, long expectedVersion)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = header;

        Assert.Equal(expected, PlanningEndpointSupport.TryReadVersion(context.Request, out var version));
        Assert.Equal(expectedVersion, version);
        PlanningEndpointSupport.WriteEtag(context.Response, 9);
        Assert.Equal("\"9\"", context.Response.Headers.ETag);
    }

    [Theory]
    [InlineData("version_conflict", 412)]
    [InlineData("note_not_found", 404)]
    [InlineData("camp_access_denied", 403)]
    [InlineData("camp_archived", 409)]
    [InlineData("provider_unavailable", 503)]
    [InlineData("provider_timeout", 503)]
    [InlineData("invalid", 400)]
    public void PlanningProblemsHaveStableStatusCodes(string errorCode, int expectedStatus)
    {
        var result = PlanningEndpointSupport.Problem(errorCode, "Fehler", "Planung");

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(PasswordAuthenticationOutcome.InvalidCredentials, 401)]
    [InlineData(PasswordAuthenticationOutcome.LockedOut, 423)]
    [InlineData(PasswordAuthenticationOutcome.RateLimited, 429)]
    public void LoginOutcomesHaveStableStatusCodes(
        PasswordAuthenticationOutcome outcome,
        int expectedStatus)
    {
        var result = InvokePrivate<IResult>(
            typeof(IdentityEndpoints),
            "PasswordLoginProblem",
            outcome);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(InvitationAcceptanceOutcome.Expired)]
    [InlineData(InvitationAcceptanceOutcome.Revoked)]
    [InlineData(InvitationAcceptanceOutcome.Used)]
    [InlineData(InvitationAcceptanceOutcome.Invalid)]
    public void InvitationFailuresHaveStableProblemDetails(InvitationAcceptanceOutcome outcome)
    {
        var result = InvokePrivate<IResult>(typeof(LifecycleEndpoints), "InvitationProblem", outcome);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public void SessionClaimsRequireBothUserAndSessionIdentifiers()
    {
        var valid = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()), new Claim("session_id", TargetId.ToString())]));
        var missingSession = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, ActorId.ToString())]));

        Assert.True(IdentityEndpoints.TryGetSession(valid, out var userId, out var sessionId));
        Assert.Equal(ActorId, userId);
        Assert.Equal(TargetId, sessionId);
        Assert.False(IdentityEndpoints.TryGetSession(missingSession, out _, out _));
    }

    private static AttachmentOwnerAuthorizationAdapter CreateAttachmentAdapter(
        ITenantAccessControl access, CampStatus status) => new(
        access,
        new CampDefaults(status),
        Proxy<ISchedulePlanning>(),
        Proxy<ICampMealPlanning>(),
        Proxy<IOrganizationCateringLibrary>(),
        Proxy<IMaterialPlanning>(),
        Proxy<IDevotionPlanning>(),
        Proxy<ICampNotebook>());

    private static T Proxy<T>() where T : class
        => DispatchProxy.Create<T, PlanningReadEndpointApiTests.ContractProxy>();

    private static T InvokePrivate<T>(Type type, string name, params object?[] arguments)
    {
        var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.Name}.{name} was not found.");
        return Assert.IsAssignableFrom<T>(method.Invoke(null, arguments));
    }

    private sealed class AccessControl(bool allowed) : ITenantAccessControl
    {
        public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
            OrganizationAccessRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(allowed
                ? TenantAccessDecision.Permit(TenantRole.Owner)
                : TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied));

        public Task<TenantAccessDecision> AuthorizeCampAsync(
            CampAccessRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(allowed
                ? TenantAccessDecision.Permit(TenantRole.Owner)
                : TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied));
    }

    private sealed class CampDefaults(CampStatus status) : ICampPlanningDefaults
    {
        public Task<CampPlanningDefaults> GetAsync(
            CampAccessQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new CampPlanningDefaults(query.CampId, 10, status, 1));
    }

    private sealed class RejectScheduleReference : IScheduleReferenceAccess
    {
        public Task<ScheduleEntryReference> RequireAsync(
            ScheduleEntryReferenceRequest request, CancellationToken cancellationToken) =>
            throw new CampsRuleException("schedule_entry_not_found", "Nicht gefunden");
    }
}

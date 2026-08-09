using Camps.Contracts;
using Catering.Contracts;
using Knowledge.Contracts;
using Logistics.Contracts;
using Spiritual.Contracts;

internal sealed class KnowledgeCampContextAdapter(ICampPlanningDefaults camps) : IKnowledgeCampContext
{
    public async Task<KnowledgeCampContext> GetAsync(
        KnowledgeCampContextRequest request,
        CancellationToken cancellationToken)
    {
        var camp = await camps.GetAsync(
            new CampAccessQuery(request.ActorId, request.OrganizationId, request.CampId),
            cancellationToken);
        return new KnowledgeCampContext(camp.Status == CampStatus.Archived);
    }
}

internal sealed class NoteLinkTargetResolver(
    ISchedulePlanning schedule,
    ICampMealPlanning meals,
    IOrganizationCateringLibrary recipes,
    IDevotionPlanning devotions,
    IMaterialPlanning materials,
    IShoppingPlanning shopping) : INoteLinkTargetResolver
{
    public async Task<IReadOnlyList<ResolvedNoteLink>> ResolveAsync(
        NoteLinkResolutionRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = new List<ResolvedNoteLink>(request.Links.Count);
        foreach (var link in request.Links.Distinct())
        {
            var target = await ResolveOneAsync(request, link, cancellationToken);
            if (target is not null) resolved.Add(target);
        }
        return resolved;
    }

    private async Task<ResolvedNoteLink?> ResolveOneAsync(
        NoteLinkResolutionRequest request,
        NoteLinkReference link,
        CancellationToken cancellationToken)
    {
        try
        {
            return link.Type switch
            {
                NoteLinkTargetType.ScheduleEntry => await ResolveScheduleAsync(request, link, cancellationToken),
                NoteLinkTargetType.Meal => await ResolveMealAsync(request, link, cancellationToken),
                NoteLinkTargetType.Recipe => await ResolveRecipeAsync(request, link, cancellationToken),
                NoteLinkTargetType.Devotion => await ResolveDevotionAsync(request, link, cancellationToken),
                NoteLinkTargetType.MaterialRequirement => await ResolveMaterialAsync(request, link, cancellationToken),
                NoteLinkTargetType.ShoppingList => await ResolveShoppingListAsync(request, link, cancellationToken),
                _ => null
            };
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<ResolvedNoteLink> ResolveScheduleAsync(
        NoteLinkResolutionRequest request, NoteLinkReference link, CancellationToken cancellationToken)
    {
        var value = await schedule.GetAsync(new ScheduleEntryQuery(request.ActorId, request.OrganizationId,
            request.CampId, link.TargetId), cancellationToken);
        return new ResolvedNoteLink(link.Type, link.TargetId, value.Title);
    }

    private async Task<ResolvedNoteLink?> ResolveMealAsync(
        NoteLinkResolutionRequest request, NoteLinkReference link, CancellationToken cancellationToken)
    {
        var value = await meals.GetMealAsync(new MealRequest(request.ActorId, request.OrganizationId,
            request.CampId, link.TargetId), cancellationToken);
        return value is null ? null : new ResolvedNoteLink(link.Type, link.TargetId, value.Name);
    }

    private async Task<ResolvedNoteLink?> ResolveRecipeAsync(
        NoteLinkResolutionRequest request, NoteLinkReference link, CancellationToken cancellationToken)
    {
        var value = await recipes.GetRecipeAsync(
            new RecipeRequest(request.ActorId, request.OrganizationId, link.TargetId), cancellationToken);
        return value is null ? null : new ResolvedNoteLink(link.Type, link.TargetId, value.CurrentVersion.Name);
    }

    private async Task<ResolvedNoteLink?> ResolveDevotionAsync(
        NoteLinkResolutionRequest request, NoteLinkReference link, CancellationToken cancellationToken)
    {
        var value = await devotions.GetAsync(new DevotionKey(request.ActorId, request.OrganizationId,
            request.CampId, link.TargetId), cancellationToken);
        return value is null ? null : new ResolvedNoteLink(link.Type, link.TargetId, value.Topic);
    }

    private async Task<ResolvedNoteLink?> ResolveMaterialAsync(
        NoteLinkResolutionRequest request, NoteLinkReference link, CancellationToken cancellationToken)
    {
        var value = await materials.GetAsync(new MaterialRequest(request.ActorId, request.OrganizationId,
            request.CampId, link.TargetId), cancellationToken);
        return value is null ? null : new ResolvedNoteLink(link.Type, link.TargetId, value.Name);
    }

    private async Task<ResolvedNoteLink?> ResolveShoppingListAsync(
        NoteLinkResolutionRequest request, NoteLinkReference link, CancellationToken cancellationToken)
    {
        var value = await shopping.GetAsync(new ShoppingListRequest(request.ActorId, request.OrganizationId,
            request.CampId, link.TargetId), cancellationToken);
        return value is null ? null : new ResolvedNoteLink(link.Type, link.TargetId, value.Name);
    }
}

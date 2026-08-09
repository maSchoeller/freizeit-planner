using Camps.Contracts;
using Catering.Contracts;
using Files.Contracts;
using Identity.Contracts;
using Knowledge.Contracts;
using Logistics.Contracts;
using Spiritual.Contracts;

internal sealed class AttachmentOwnerAuthorizationAdapter(
    ITenantAccessControl access,
    ISchedulePlanning schedule,
    ICampMealPlanning meals,
    IOrganizationCateringLibrary recipes,
    IMaterialPlanning materials,
    IDevotionPlanning devotions,
    ICampNotebook notes) : IAttachmentOwnerAuthorization
{
    public async Task<AttachmentOwnerAccessDecision> AuthorizeAsync(
        AttachmentOwnerAccessRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Owner.Type == AttachmentOwnerType.Recipe)
            {
                return await AuthorizeRecipeAsync(request, cancellationToken);
            }
            if (request.CampId is not { } campId) return AttachmentOwnerAccessDecision.Deny();
            var action = request.Action == AttachmentOwnerAction.Read ? CampAction.Read : CampAction.WriteContent;
            var decision = await access.AuthorizeCampAsync(
                new CampAccessRequest(request.ActorId, request.OrganizationId, campId, action), cancellationToken);
            if (!decision.Allowed || !await OwnerExistsAsync(request, campId, cancellationToken))
                return AttachmentOwnerAccessDecision.Deny();
            return AttachmentOwnerAccessDecision.Permit(new AttachmentOwnerScope(
                request.OrganizationId, campId, AttachmentQuotaScopeType.Camp));
        }
        catch (InvalidOperationException)
        {
            return AttachmentOwnerAccessDecision.Deny();
        }
    }

    private async Task<AttachmentOwnerAccessDecision> AuthorizeRecipeAsync(
        AttachmentOwnerAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CampId is not null) return AttachmentOwnerAccessDecision.Deny();
        var action = request.Action == AttachmentOwnerAction.Read
            ? OrganizationAction.Read
            : OrganizationAction.ManageCamps;
        var decision = await access.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(request.ActorId, request.OrganizationId, action), cancellationToken);
        if (!decision.Allowed) return AttachmentOwnerAccessDecision.Deny();
        var recipe = await recipes.GetRecipeAsync(
            new RecipeRequest(request.ActorId, request.OrganizationId, request.Owner.Id), cancellationToken);
        return recipe is null
            ? AttachmentOwnerAccessDecision.Deny()
            : AttachmentOwnerAccessDecision.Permit(new AttachmentOwnerScope(
                request.OrganizationId, null, AttachmentQuotaScopeType.OrganizationRecipeLibrary));
    }

    private async Task<bool> OwnerExistsAsync(
        AttachmentOwnerAccessRequest request,
        Guid campId,
        CancellationToken cancellationToken) => request.Owner.Type switch
        {
            AttachmentOwnerType.ScheduleEntry => await schedule.GetAsync(new ScheduleEntryQuery(request.ActorId,
                request.OrganizationId, campId, request.Owner.Id), cancellationToken) is not null,
            AttachmentOwnerType.Meal => await meals.GetMealAsync(new MealRequest(request.ActorId,
                request.OrganizationId, campId, request.Owner.Id), cancellationToken) is not null,
            AttachmentOwnerType.MaterialRequirement => await materials.GetAsync(new MaterialRequest(request.ActorId,
                request.OrganizationId, campId, request.Owner.Id), cancellationToken) is not null,
            AttachmentOwnerType.Devotion => await devotions.GetAsync(new DevotionKey(request.ActorId,
                request.OrganizationId, campId, request.Owner.Id), cancellationToken) is not null,
            AttachmentOwnerType.Note => await notes.GetNoteAsync(new NoteRequest(request.ActorId,
                request.OrganizationId, campId, request.Owner.Id), cancellationToken) is not null,
            _ => false
        };
}

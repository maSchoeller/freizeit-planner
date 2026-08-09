using Camps.Contracts;
using Catering.Contracts;

internal sealed class CampCateringContextAdapter(
    ICampPlanningDefaults campDefaults,
    IScheduleReferenceAccess scheduleReferences)
    : ICampCateringContext
{
    public async Task<CampCateringContext> GetAsync(
        CampCateringContextRequest request,
        CancellationToken cancellationToken)
    {
        var result = await campDefaults.GetAsync(
            new CampAccessQuery(request.ActorId, request.OrganizationId, request.CampId),
            cancellationToken);
        return new CampCateringContext(
            result.DefaultPortions,
            result.Status == CampStatus.Archived);
    }

    public async Task<bool> IsScheduleEntryWritableAsync(
        CampCateringScheduleReference request,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await scheduleReferences.RequireAsync(
                new ScheduleEntryReferenceRequest(
                    request.ActorId,
                    request.OrganizationId,
                    request.CampId,
                    request.ScheduleEntryId,
                    ScheduleReferencePurpose.LinkForWrite),
                cancellationToken);
            return true;
        }
        catch (CampsRuleException)
        {
            return false;
        }
    }
}

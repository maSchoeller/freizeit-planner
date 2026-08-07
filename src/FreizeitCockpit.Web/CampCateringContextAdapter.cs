using Camps.Contracts;
using Catering.Contracts;

internal sealed class CampCateringContextAdapter(ICampPlanningDefaults campDefaults)
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
}

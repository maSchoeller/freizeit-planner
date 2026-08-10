using Identity.Contracts;
using Xunit;

namespace Identity.Tests;

public sealed class TransferableInvitationContractTests
{
    [Fact]
    public void InvitationGrantSeparatesGlobalOrganizationAndCampRoles()
    {
        var global = InvitationGrant.SuperAdmin();
        var organizationId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        var organization = InvitationGrant.ForOrganizationAdmin(organizationId);
        var camp = InvitationGrant.ForCamp(organizationId, campId, CampRole.Member);

        Assert.True(global.IsSuperAdmin);
        Assert.Null(global.OrganizationId);
        Assert.Equal(OrganizationRole.OrganizationAdmin, organization.OrganizationRole);
        Assert.Equal(CampRole.Member, camp.CampRole);
        Assert.Equal(campId, camp.CampId);
    }

    [Fact]
    public void TransferableInvitationRequestContainsNoEmailAddress()
    {
        var propertyNames = typeof(CreateInvitationLinkRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name =>
            name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }
}

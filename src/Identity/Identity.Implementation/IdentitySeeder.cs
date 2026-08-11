using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Identity.Implementation;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IdentityDbContext dbContext,
        bool includeDevelopmentData,
        CancellationToken cancellationToken = default)
    {
        if (!includeDevelopmentData)
        {
            return;
        }

        var users = new[]
        {
            CreateUser(Guid.Parse("10000000-0000-0000-0000-000000000001"), "miriam@example.test", "Miriam König"),
            CreateUser(Guid.Parse("10000000-0000-0000-0000-000000000002"), "admin@example.test", "Organization Admin"),
            CreateUser(Guid.Parse("10000000-0000-0000-0000-000000000003"), "camp-lead@example.test", "Camp-Leitung"),
            CreateUser(Guid.Parse("10000000-0000-0000-0000-000000000004"), "member@example.test", "Teammitglied"),
            CreateUser(Guid.Parse("10000000-0000-0000-0000-000000000005"), "viewer@example.test", "Lesender Zugriff"),
            CreateUser(Guid.Parse("10000000-0000-0000-0000-000000000006"), "superadmin@example.test", "Superadmin", true)
        };
        foreach (var user in users)
        {
            if (!await dbContext.Users.AnyAsync(item => item.Id == user.Id, cancellationToken))
            {
                dbContext.Users.Add(user);
            }
        }

        var organizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        if (!await dbContext.Organizations.AnyAsync(item => item.Id == organizationId, cancellationToken))
        {
            dbContext.Organizations.Add(new OrganizationEntity
            {
                Id = organizationId,
                Name = "CVJM Sonnenhöhe",
                Slug = "sonnenhoehe",
                Status = OrganizationStatus.Active,
                Version = 1
            });
        }

        var organizationRoles = new[]
        {
            new MembershipEntity
            {
                OrganizationId = organizationId,
                UserId = users[0].Id,
                Role = TenantRole.OrganizationAdmin,
                IsActive = true,
                Status = MembershipStatus.Active,
                OrganizationRole = OrganizationRole.OrganizationAdmin
            },
            new MembershipEntity
            {
                OrganizationId = organizationId,
                UserId = users[1].Id,
                Role = TenantRole.OrganizationAdmin,
                IsActive = true,
                Status = MembershipStatus.Active,
                OrganizationRole = OrganizationRole.OrganizationAdmin
            },
            new MembershipEntity
            {
                OrganizationId = organizationId,
                UserId = users[2].Id,
                Role = TenantRole.Viewer,
                IsActive = true,
                Status = MembershipStatus.Active
            },
            new MembershipEntity
            {
                OrganizationId = organizationId,
                UserId = users[3].Id,
                Role = TenantRole.Viewer,
                IsActive = true,
                Status = MembershipStatus.Active
            },
            new MembershipEntity
            {
                OrganizationId = organizationId,
                UserId = users[4].Id,
                Role = TenantRole.Viewer,
                IsActive = true,
                Status = MembershipStatus.Active
            }
        };
        foreach (var membership in organizationRoles)
        {
            if (!await dbContext.Memberships.AnyAsync(
                    item => item.OrganizationId == membership.OrganizationId && item.UserId == membership.UserId,
                    cancellationToken))
            {
                dbContext.Memberships.Add(membership);
            }
        }

        var campId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var campRoles = new[]
        {
            new CampAssignmentEntity
            {
                OrganizationId = organizationId,
                CampId = campId,
                UserId = users[2].Id,
                Role = TenantRole.CampLead,
                CampRole = CampRole.CampLead
            },
            new CampAssignmentEntity
            {
                OrganizationId = organizationId,
                CampId = campId,
                UserId = users[3].Id,
                Role = TenantRole.Member,
                CampRole = CampRole.Member
            },
            new CampAssignmentEntity
            {
                OrganizationId = organizationId,
                CampId = campId,
                UserId = users[4].Id,
                Role = TenantRole.Viewer,
                CampRole = CampRole.Viewer
            }
        };
        foreach (var assignment in campRoles)
        {
            if (!await dbContext.CampAssignments.AnyAsync(
                    item => item.CampId == assignment.CampId && item.UserId == assignment.UserId,
                    cancellationToken))
            {
                dbContext.CampAssignments.Add(assignment);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ApplicationUser CreateUser(
        Guid id,
        string email,
        string displayName,
        bool isSuperAdmin = false) => new()
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            DisplayName = displayName,
            FirstName = displayName,
            LastName = string.Empty,
            IsSuperAdmin = isSuperAdmin,
            SecurityStamp = id.ToString("N")
        };
}

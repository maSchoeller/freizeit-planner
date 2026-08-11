using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catering.Implementation.Migrations;

[DbContext(typeof(CateringDbContext))]
[Migration("20260811000003_EnforceCampAssignmentsRls")]
public sealed class EnforceCampAssignmentsRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => SetPolicies(migrationBuilder, true);

    protected override void Down(MigrationBuilder migrationBuilder) => SetPolicies(migrationBuilder, false);

    private static void SetPolicies(MigrationBuilder migrationBuilder, bool requireAssignment)
    {
        var accessFunction = requireAssignment
            ? "identity.runtime_can_access_camp(organization_id, camp_id)"
            : "identity.runtime_can_access_organization(organization_id)";
        foreach (var table in new[] { "meals", "recipe_snapshots", "snapshot_ingredients" })
        {
            migrationBuilder.Sql($$"""
                ALTER POLICY catering_camp_access ON catering.{{table}}
                    USING (
                        {{accessFunction}}
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
                    WITH CHECK (
                        {{accessFunction}}
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);
                """);
        }
    }
}

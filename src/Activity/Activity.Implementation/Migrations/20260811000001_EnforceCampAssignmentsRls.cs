using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Activity.Implementation.Migrations;

[DbContext(typeof(ActivityDbContext))]
[Migration("20260811000001_EnforceCampAssignmentsRls")]
public sealed class EnforceCampAssignmentsRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER POLICY activity_camp_access ON activity.activity_events
            USING (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

        ALTER POLICY activity_camp_access ON activity.search_documents
            USING (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER POLICY activity_camp_access ON activity.activity_events
            USING (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

        ALTER POLICY activity_camp_access ON activity.search_documents
            USING (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);
        """);
}

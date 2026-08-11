using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowledge.Implementation.Migrations;

[DbContext(typeof(KnowledgeDbContext))]
[Migration("20260811000002_EnforceCampAssignmentsRls")]
public sealed class EnforceCampAssignmentsRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER POLICY knowledge_camp_access ON knowledge.notes
            USING (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

        ALTER POLICY knowledge_camp_access ON knowledge.note_tags
            USING (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

        ALTER POLICY knowledge_camp_access ON knowledge.note_links
            USING (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_camp(organization_id, camp_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER POLICY knowledge_camp_access ON knowledge.notes
            USING (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);
        ALTER POLICY knowledge_camp_access ON knowledge.note_tags
            USING (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);
        ALTER POLICY knowledge_camp_access ON knowledge.note_links
            USING (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
            WITH CHECK (
                identity.runtime_can_access_organization(organization_id)
                AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);
        """);
}

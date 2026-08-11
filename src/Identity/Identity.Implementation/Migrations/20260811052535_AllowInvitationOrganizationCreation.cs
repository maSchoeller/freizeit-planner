using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AllowInvitationOrganizationCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER POLICY organizations_select ON identity.organizations
                    USING (
                        current_setting('app.operation', true) = 'invitation_acceptance'
                        OR (
                            current_setting('app.operation', true) <> 'platform_admin'
                            AND identity.runtime_can_access_organization("Id")
                        )
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    );
                ALTER POLICY organizations_insert ON identity.organizations
                    WITH CHECK (
                        identity.runtime_can_access_organization("Id")
                        OR current_setting('app.operation', true) IN (
                            'platform_create_organization',
                            'invitation_acceptance'
                        )
                    );
                ALTER POLICY camp_assignments_isolation ON identity.camp_assignments
                    USING (
                        identity.runtime_can_access_camp_assignment(organization_id, camp_id, user_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    )
                    WITH CHECK (
                        identity.runtime_can_access_camp_assignment(organization_id, camp_id, user_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER POLICY organizations_select ON identity.organizations
                    USING (
                        (
                            current_setting('app.operation', true) <> 'platform_admin'
                            AND identity.runtime_can_access_organization("Id")
                        )
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    );
                ALTER POLICY organizations_insert ON identity.organizations
                    WITH CHECK (
                        identity.runtime_can_access_organization("Id")
                        OR current_setting('app.operation', true) = 'platform_create_organization'
                    );
                ALTER POLICY camp_assignments_isolation ON identity.camp_assignments
                    USING (
                        identity.runtime_can_access_camp_assignment(organization_id, camp_id, user_id)
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    )
                    WITH CHECK (
                        identity.runtime_can_access_camp_assignment(organization_id, camp_id, user_id)
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    );
                """);
        }
    }
}

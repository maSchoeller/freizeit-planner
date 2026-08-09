using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddCampMemberDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION identity.list_camp_members(
                    target_organization_id uuid,
                    target_camp_id uuid)
                RETURNS TABLE ("UserId" uuid, "DisplayName" text)
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity
                AS $function$
                    SELECT membership.user_id, account."DisplayName"
                    FROM identity.memberships AS membership
                    INNER JOIN identity.users AS account ON account."Id" = membership.user_id
                    WHERE membership.organization_id = target_organization_id
                        AND membership."IsActive"
                        AND identity.runtime_can_access_organization(target_organization_id)
                        AND EXISTS (
                            SELECT 1
                            FROM identity.memberships AS actor_membership
                            WHERE actor_membership.organization_id = target_organization_id
                                AND actor_membership.user_id = NULLIF(
                                    current_setting('app.user_id', true), '')::uuid
                                AND actor_membership."IsActive"
                                AND (
                                    actor_membership."Role" IN (0, 1)
                                    OR EXISTS (
                                        SELECT 1
                                        FROM identity.camp_assignments AS actor_assignment
                                        WHERE actor_assignment.organization_id = target_organization_id
                                            AND actor_assignment.camp_id = target_camp_id
                                            AND actor_assignment.user_id = actor_membership.user_id
                                            AND actor_assignment."IsActive"
                                    )
                                )
                        )
                        AND (
                            membership."Role" IN (0, 1)
                            OR EXISTS (
                                SELECT 1
                                FROM identity.camp_assignments AS target_assignment
                                WHERE target_assignment.organization_id = target_organization_id
                                    AND target_assignment.camp_id = target_camp_id
                                    AND target_assignment.user_id = membership.user_id
                                    AND target_assignment."IsActive"
                            )
                        )
                    ORDER BY account."DisplayName", account."Id";
                $function$;

                REVOKE ALL ON FUNCTION identity.list_camp_members(uuid, uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION identity.list_camp_members(uuid, uuid) TO freizeit_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS identity.list_camp_members(uuid, uuid);");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationMembershipAndCampRolesV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationRole",
                schema: "identity",
                table: "memberships",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "identity",
                table: "memberships",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CampRole",
                schema: "identity",
                table: "camp_assignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE identity.users
                SET "FirstName" = "DisplayName"
                WHERE "FirstName" = ''
                    AND "LastName" = ''
                    AND "DisplayName" <> '';

                UPDATE identity.memberships
                SET "Status" = CASE WHEN "IsActive" THEN 0 ELSE 2 END,
                    "OrganizationRole" = CASE WHEN "Role" IN (0, 1) THEN 0 ELSE NULL END;

                UPDATE identity.camp_assignments
                SET "CampRole" = CASE "Role"
                    WHEN 2 THEN 0
                    WHEN 3 THEN 1
                    ELSE 2
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION identity.runtime_can_access_organization(target_organization_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity
                AS $function$
                    SELECT
                        (
                            NULLIF(current_setting('app.organization_id', true), '') IS NULL
                            OR NULLIF(current_setting('app.organization_id', true), '')::uuid = target_organization_id
                        )
                        AND EXISTS (
                            SELECT 1
                            FROM identity.memberships AS membership
                            INNER JOIN identity.organizations AS organization
                                ON organization."Id" = membership.organization_id
                            INNER JOIN identity.users AS app_user
                                ON app_user."Id" = membership.user_id
                            WHERE membership.organization_id = target_organization_id
                                AND membership.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                AND membership."Status" = 0
                                AND organization."Status" = 0
                                AND app_user."AccountStatus" = 0
                        );
                $function$;

                CREATE OR REPLACE FUNCTION identity.runtime_is_platform_admin()
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity
                AS $function$
                    SELECT EXISTS (
                        SELECT 1
                        FROM identity.users AS app_user
                        WHERE app_user."Id" = NULLIF(current_setting('app.user_id', true), '')::uuid
                            AND app_user."IsSuperAdmin"
                            AND app_user."AccountStatus" = 0
                    );
                $function$;

                CREATE OR REPLACE FUNCTION identity.runtime_is_organization_manager(target_organization_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity
                AS $function$
                    SELECT identity.runtime_can_access_organization(target_organization_id)
                        AND EXISTS (
                            SELECT 1
                            FROM identity.memberships AS actor_membership
                            WHERE actor_membership.organization_id = target_organization_id
                                AND actor_membership.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                AND actor_membership."Status" = 0
                                AND actor_membership."OrganizationRole" = 0
                        );
                $function$;

                CREATE OR REPLACE FUNCTION identity.runtime_can_view_members(target_organization_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity
                AS $function$
                    SELECT identity.runtime_is_organization_manager(target_organization_id)
                        OR (
                            identity.runtime_can_access_organization(target_organization_id)
                            AND EXISTS (
                                SELECT 1
                                FROM identity.camp_assignments AS actor_assignment
                                WHERE actor_assignment.organization_id = target_organization_id
                                    AND actor_assignment.camp_id = NULLIF(current_setting('app.camp_id', true), '')::uuid
                                    AND actor_assignment.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                    AND actor_assignment."IsActive"
                                    AND actor_assignment."CampRole" = 0
                            )
                        );
                $function$;

                CREATE OR REPLACE FUNCTION identity.runtime_can_access_camp_assignment(
                    target_organization_id uuid,
                    target_camp_id uuid,
                    target_user_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity
                AS $function$
                    SELECT identity.runtime_can_access_organization(target_organization_id)
                        AND (
                            target_user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                            OR EXISTS (
                                SELECT 1
                                FROM identity.memberships AS actor_membership
                                WHERE actor_membership.organization_id = target_organization_id
                                    AND actor_membership.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                    AND actor_membership."Status" = 0
                                    AND actor_membership."OrganizationRole" = 0
                            )
                            OR (
                                target_camp_id = NULLIF(current_setting('app.camp_id', true), '')::uuid
                                AND EXISTS (
                                    SELECT 1
                                    FROM identity.camp_assignments AS actor_assignment
                                    WHERE actor_assignment.organization_id = target_organization_id
                                        AND actor_assignment.camp_id = target_camp_id
                                        AND actor_assignment.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                        AND actor_assignment."IsActive"
                                        AND actor_assignment."CampRole" = 0
                                )
                            )
                        );
                $function$;

                CREATE OR REPLACE FUNCTION identity.runtime_can_access_camp(
                    target_organization_id uuid,
                    target_camp_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity
                AS $function$
                    SELECT identity.runtime_is_organization_manager(target_organization_id)
                        OR (
                            identity.runtime_can_access_organization(target_organization_id)
                            AND EXISTS (
                                SELECT 1
                                FROM identity.camp_assignments AS actor_assignment
                                WHERE actor_assignment.organization_id = target_organization_id
                                    AND actor_assignment.camp_id = target_camp_id
                                    AND actor_assignment.user_id = NULLIF(
                                        current_setting('app.user_id', true), '')::uuid
                                    AND actor_assignment."IsActive"
                            )
                        );
                $function$;

                REVOKE ALL ON FUNCTION identity.runtime_can_access_camp(uuid, uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION identity.runtime_can_access_camp(uuid, uuid) TO freizeit_app;
                """);

            migrationBuilder.Sql("""
                ALTER POLICY memberships_select ON identity.memberships
                    USING (
                        (
                            identity.runtime_can_access_organization(organization_id)
                            AND user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                        )
                        OR identity.runtime_can_view_members(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    );
                ALTER POLICY memberships_insert ON identity.memberships
                    WITH CHECK (
                        identity.runtime_is_organization_manager(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    );
                ALTER POLICY memberships_update ON identity.memberships
                    USING (
                        identity.runtime_is_organization_manager(organization_id)
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    )
                    WITH CHECK (
                        identity.runtime_is_organization_manager(organization_id)
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
                    );
                ALTER POLICY memberships_delete ON identity.memberships
                    USING (
                        identity.runtime_is_organization_manager(organization_id)
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                        )
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER POLICY memberships_select ON identity.memberships
                    USING (
                        (
                            identity.runtime_can_access_organization(organization_id)
                            AND user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                        )
                        OR identity.runtime_can_view_members(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                    );
                ALTER POLICY memberships_insert ON identity.memberships
                    WITH CHECK (
                        identity.runtime_is_organization_manager(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                    );
                ALTER POLICY memberships_update ON identity.memberships
                    USING (identity.runtime_is_organization_manager(organization_id))
                    WITH CHECK (identity.runtime_is_organization_manager(organization_id));
                ALTER POLICY memberships_delete ON identity.memberships
                    USING (identity.runtime_is_organization_manager(organization_id));
                ALTER POLICY camp_assignments_isolation ON identity.camp_assignments
                    USING (identity.runtime_can_access_camp_assignment(organization_id, camp_id, user_id))
                    WITH CHECK (identity.runtime_can_access_camp_assignment(organization_id, camp_id, user_id));
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION identity.runtime_can_access_organization(target_organization_id uuid)
                RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, identity
                AS $function$
                    SELECT
                        (
                            NULLIF(current_setting('app.organization_id', true), '') IS NULL
                            OR NULLIF(current_setting('app.organization_id', true), '')::uuid = target_organization_id
                        )
                        AND EXISTS (
                            SELECT 1 FROM identity.memberships AS membership
                            INNER JOIN identity.organizations AS organization ON organization."Id" = membership.organization_id
                            INNER JOIN identity.users AS app_user ON app_user."Id" = membership.user_id
                            WHERE membership.organization_id = target_organization_id
                                AND membership.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                AND membership."IsActive" AND organization."Status" = 0
                                AND NOT app_user."IsPlatformAdmin"
                        );
                $function$;
                CREATE OR REPLACE FUNCTION identity.runtime_is_platform_admin()
                RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, identity
                AS $function$
                    SELECT EXISTS (
                        SELECT 1 FROM identity.users AS app_user
                        WHERE app_user."Id" = NULLIF(current_setting('app.user_id', true), '')::uuid
                            AND app_user."IsPlatformAdmin"
                    );
                $function$;
                CREATE OR REPLACE FUNCTION identity.runtime_is_organization_manager(target_organization_id uuid)
                RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, identity
                AS $function$
                    SELECT identity.runtime_can_access_organization(target_organization_id)
                        AND EXISTS (
                            SELECT 1 FROM identity.memberships AS actor_membership
                            WHERE actor_membership.organization_id = target_organization_id
                                AND actor_membership.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                AND actor_membership."IsActive" AND actor_membership."Role" IN (0, 1)
                        );
                $function$;
                CREATE OR REPLACE FUNCTION identity.runtime_can_view_members(target_organization_id uuid)
                RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, identity
                AS $function$
                    SELECT identity.runtime_is_organization_manager(target_organization_id)
                        OR (
                            identity.runtime_can_access_organization(target_organization_id)
                            AND EXISTS (
                                SELECT 1 FROM identity.camp_assignments AS actor_assignment
                                WHERE actor_assignment.organization_id = target_organization_id
                                    AND actor_assignment.camp_id = NULLIF(current_setting('app.camp_id', true), '')::uuid
                                    AND actor_assignment.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                    AND actor_assignment."IsActive" AND actor_assignment."Role" = 2
                            )
                        );
                $function$;
                CREATE OR REPLACE FUNCTION identity.runtime_can_access_camp_assignment(
                    target_organization_id uuid, target_camp_id uuid, target_user_id uuid)
                RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, identity
                AS $function$
                    SELECT identity.runtime_can_access_organization(target_organization_id)
                        AND (
                            target_user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                            OR EXISTS (
                                SELECT 1 FROM identity.memberships AS actor_membership
                                WHERE actor_membership.organization_id = target_organization_id
                                    AND actor_membership.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                    AND actor_membership."IsActive" AND actor_membership."Role" IN (0, 1)
                            )
                            OR (
                                target_camp_id = NULLIF(current_setting('app.camp_id', true), '')::uuid
                                AND EXISTS (
                                    SELECT 1 FROM identity.camp_assignments AS actor_assignment
                                    WHERE actor_assignment.organization_id = target_organization_id
                                        AND actor_assignment.camp_id = target_camp_id
                                        AND actor_assignment.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                                        AND actor_assignment."IsActive" AND actor_assignment."Role" = 2
                                )
                            )
                        );
                $function$;
                """);

            migrationBuilder.DropColumn(
                name: "OrganizationRole",
                schema: "identity",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "identity",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "CampRole",
                schema: "identity",
                table: "camp_assignments");
        }
    }
}

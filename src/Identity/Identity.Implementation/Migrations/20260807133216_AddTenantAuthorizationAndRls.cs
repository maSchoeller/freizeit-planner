using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAuthorizationAndRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "identity",
                table: "memberships",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "identity",
                table: "invitations",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "identity",
                table: "camp_assignments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "identity",
                table: "camp_assignments",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql(
                """
                DO $role$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'freizeit_app') THEN
                        CREATE ROLE freizeit_app NOLOGIN NOBYPASSRLS;
                    ELSE
                        ALTER ROLE freizeit_app NOBYPASSRLS;
                    END IF;
                END
                $role$;

                GRANT USAGE ON SCHEMA identity TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO freizeit_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO freizeit_app;

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
                                AND membership."IsActive"
                                AND organization."Status" = 0
                                AND NOT app_user."IsPlatformAdmin"
                        );
                $function$;

                REVOKE ALL ON FUNCTION identity.runtime_can_access_organization(uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION identity.runtime_can_access_organization(uuid) TO freizeit_app;

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
                            AND app_user."IsPlatformAdmin"
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
                                AND actor_membership."IsActive"
                                AND actor_membership."Role" IN (0, 1)
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
                                    AND actor_assignment."Role" = 2
                            )
                        );
                $function$;

                REVOKE ALL ON FUNCTION identity.runtime_is_platform_admin() FROM PUBLIC;
                REVOKE ALL ON FUNCTION identity.runtime_is_organization_manager(uuid) FROM PUBLIC;
                REVOKE ALL ON FUNCTION identity.runtime_can_view_members(uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION identity.runtime_is_platform_admin() TO freizeit_app;
                GRANT EXECUTE ON FUNCTION identity.runtime_is_organization_manager(uuid) TO freizeit_app;
                GRANT EXECUTE ON FUNCTION identity.runtime_can_view_members(uuid) TO freizeit_app;

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
                                    AND actor_membership."IsActive"
                                    AND actor_membership."Role" IN (0, 1)
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
                                        AND actor_assignment."Role" = 2
                                )
                            )
                        );
                $function$;

                REVOKE ALL ON FUNCTION identity.runtime_can_access_camp_assignment(uuid, uuid, uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION identity.runtime_can_access_camp_assignment(uuid, uuid, uuid) TO freizeit_app;

                ALTER TABLE identity.organizations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.organizations FORCE ROW LEVEL SECURITY;
                ALTER TABLE identity.memberships ENABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.memberships FORCE ROW LEVEL SECURITY;
                ALTER TABLE identity.camp_assignments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.camp_assignments FORCE ROW LEVEL SECURITY;
                ALTER TABLE identity.invitations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.invitations FORCE ROW LEVEL SECURITY;

                CREATE POLICY organizations_select ON identity.organizations
                    FOR SELECT TO freizeit_app
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
                CREATE POLICY organizations_insert ON identity.organizations
                    FOR INSERT TO freizeit_app
                    WITH CHECK (
                        identity.runtime_can_access_organization("Id")
                        OR current_setting('app.operation', true) = 'platform_create_organization'
                    );
                CREATE POLICY organizations_update ON identity.organizations
                    FOR UPDATE TO freizeit_app
                    USING (
                        (
                            current_setting('app.operation', true) <> 'platform_admin'
                            AND identity.runtime_can_access_organization("Id")
                        )
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                            AND "Id" = NULLIF(current_setting('app.organization_id', true), '')::uuid
                        )
                    )
                    WITH CHECK (
                        (
                            current_setting('app.operation', true) <> 'platform_admin'
                            AND identity.runtime_can_access_organization("Id")
                        )
                        OR (
                            current_setting('app.operation', true) = 'platform_admin'
                            AND identity.runtime_is_platform_admin()
                            AND "Id" = NULLIF(current_setting('app.organization_id', true), '')::uuid
                        )
                    );
                CREATE POLICY organizations_delete ON identity.organizations
                    FOR DELETE TO freizeit_app
                    USING (identity.runtime_can_access_organization("Id"));

                CREATE POLICY memberships_select ON identity.memberships
                    FOR SELECT TO freizeit_app
                    USING (
                        (
                            identity.runtime_can_access_organization(organization_id)
                            AND user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
                        )
                        OR identity.runtime_can_view_members(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                    );
                CREATE POLICY memberships_insert ON identity.memberships
                    FOR INSERT TO freizeit_app
                    WITH CHECK (
                        identity.runtime_is_organization_manager(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                    );
                CREATE POLICY memberships_update ON identity.memberships
                    FOR UPDATE TO freizeit_app
                    USING (identity.runtime_is_organization_manager(organization_id))
                    WITH CHECK (identity.runtime_is_organization_manager(organization_id));
                CREATE POLICY memberships_delete ON identity.memberships
                    FOR DELETE TO freizeit_app
                    USING (identity.runtime_is_organization_manager(organization_id));

                CREATE POLICY camp_assignments_isolation ON identity.camp_assignments
                    FOR ALL TO freizeit_app
                    USING (identity.runtime_can_access_camp_assignment(organization_id, camp_id, user_id))
                    WITH CHECK (identity.runtime_can_access_camp_assignment(organization_id, camp_id, user_id));

                CREATE POLICY invitations_select ON identity.invitations
                    FOR SELECT TO freizeit_app
                    USING (
                        identity.runtime_is_organization_manager(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                    );
                CREATE POLICY invitations_insert ON identity.invitations
                    FOR INSERT TO freizeit_app
                    WITH CHECK (
                        identity.runtime_is_organization_manager(organization_id)
                        OR current_setting('app.operation', true) = 'platform_create_organization'
                    );
                CREATE POLICY invitations_update ON identity.invitations
                    FOR UPDATE TO freizeit_app
                    USING (
                        identity.runtime_is_organization_manager(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                    )
                    WITH CHECK (
                        identity.runtime_is_organization_manager(organization_id)
                        OR current_setting('app.operation', true) = 'invitation_acceptance'
                    );
                CREATE POLICY invitations_delete ON identity.invitations
                    FOR DELETE TO freizeit_app
                    USING (identity.runtime_is_organization_manager(organization_id));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS invitations_delete ON identity.invitations;
                DROP POLICY IF EXISTS invitations_update ON identity.invitations;
                DROP POLICY IF EXISTS invitations_insert ON identity.invitations;
                DROP POLICY IF EXISTS invitations_select ON identity.invitations;
                DROP POLICY IF EXISTS camp_assignments_isolation ON identity.camp_assignments;
                DROP POLICY IF EXISTS memberships_delete ON identity.memberships;
                DROP POLICY IF EXISTS memberships_update ON identity.memberships;
                DROP POLICY IF EXISTS memberships_insert ON identity.memberships;
                DROP POLICY IF EXISTS memberships_select ON identity.memberships;
                DROP POLICY IF EXISTS organizations_delete ON identity.organizations;
                DROP POLICY IF EXISTS organizations_update ON identity.organizations;
                DROP POLICY IF EXISTS organizations_insert ON identity.organizations;
                DROP POLICY IF EXISTS organizations_select ON identity.organizations;
                ALTER TABLE identity.invitations DISABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.camp_assignments DISABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.memberships DISABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.organizations DISABLE ROW LEVEL SECURITY;
                DROP FUNCTION IF EXISTS identity.runtime_can_access_camp_assignment(uuid, uuid, uuid);
                DROP FUNCTION IF EXISTS identity.runtime_can_view_members(uuid);
                DROP FUNCTION IF EXISTS identity.runtime_is_organization_manager(uuid);
                DROP FUNCTION IF EXISTS identity.runtime_is_platform_admin();
                DROP FUNCTION IF EXISTS identity.runtime_can_access_organization(uuid);
                """);

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "identity",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "identity",
                table: "camp_assignments");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "identity",
                table: "camp_assignments");
        }
    }
}

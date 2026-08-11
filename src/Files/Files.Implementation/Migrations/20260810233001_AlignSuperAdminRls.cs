using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Files.Implementation.Migrations;

[DbContext(typeof(FilesDbContext))]
[Migration("20260810233001_AlignSuperAdminRls")]
public sealed class AlignSuperAdminRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION files.runtime_can_access_scope(
            target_organization_id uuid,
            target_camp_id uuid,
            require_write boolean)
        RETURNS boolean
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = pg_catalog
        AS $function$
            SELECT
                nullif(current_setting('app.user_id', true), '')::uuid IS NOT NULL
                AND nullif(current_setting('app.organization_id', true), '')::uuid = target_organization_id
                AND (target_camp_id IS NULL
                     OR nullif(current_setting('app.camp_id', true), '')::uuid = target_camp_id)
                AND EXISTS (
                    SELECT 1
                    FROM identity.organizations AS organizations
                    JOIN identity.memberships AS memberships
                      ON memberships.organization_id = organizations."Id"
                    JOIN identity.users AS users
                      ON users."Id" = memberships.user_id
                    WHERE organizations."Id" = target_organization_id
                      AND organizations."Status" = 0
                      AND users."AccountStatus" = 0
                      AND memberships.user_id = nullif(current_setting('app.user_id', true), '')::uuid
                      AND memberships."Status" = 0
                      AND CASE
                          WHEN target_camp_id IS NULL THEN
                              NOT require_write OR memberships."OrganizationRole" = 0
                          WHEN memberships."OrganizationRole" = 0 THEN
                              true
                          ELSE EXISTS (
                              SELECT 1
                              FROM identity.camp_assignments AS assignments
                              WHERE assignments.organization_id = target_organization_id
                                AND assignments.camp_id = target_camp_id
                                AND assignments.user_id = memberships.user_id
                                AND assignments."IsActive"
                                AND (NOT require_write OR assignments."CampRole" IN (0, 1)))
                      END);
        $function$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION files.runtime_can_access_scope(
            target_organization_id uuid,
            target_camp_id uuid,
            require_write boolean)
        RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog
        AS $function$
            SELECT
                nullif(current_setting('app.user_id', true), '')::uuid IS NOT NULL
                AND nullif(current_setting('app.organization_id', true), '')::uuid = target_organization_id
                AND (target_camp_id IS NULL
                     OR nullif(current_setting('app.camp_id', true), '')::uuid = target_camp_id)
                AND NOT EXISTS (
                    SELECT 1 FROM identity.users AS users
                    WHERE users."Id" = nullif(current_setting('app.user_id', true), '')::uuid
                      AND users."IsPlatformAdmin")
                AND EXISTS (
                    SELECT 1 FROM identity.organizations AS organizations
                    JOIN identity.memberships AS memberships ON memberships.organization_id = organizations."Id"
                    WHERE organizations."Id" = target_organization_id
                      AND organizations."Status" = 0
                      AND memberships.user_id = nullif(current_setting('app.user_id', true), '')::uuid
                      AND memberships."IsActive"
                      AND CASE
                          WHEN target_camp_id IS NULL THEN
                              NOT require_write OR memberships."Role" IN (0, 1)
                          WHEN memberships."Role" IN (0, 1) THEN
                              true
                          ELSE EXISTS (
                              SELECT 1 FROM identity.camp_assignments AS assignments
                              WHERE assignments.organization_id = target_organization_id
                                AND assignments.camp_id = target_camp_id
                                AND assignments.user_id = memberships.user_id
                                AND assignments."IsActive"
                                AND (NOT require_write OR assignments."Role" IN (2, 3)))
                      END);
        $function$;
        """);
}

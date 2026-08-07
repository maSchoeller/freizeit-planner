using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Files.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class InitialFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "files");

            migrationBuilder.CreateTable(
                name: "attachments",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaScope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PurgeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.Id);
                    table.CheckConstraint("CK_attachments_lifecycle", "(\"State\" IN ('PendingUpload', 'Available') AND \"DeletedAt\" IS NULL AND \"PurgeAt\" IS NULL) OR (\"State\" = 'Deleted' AND \"DeletedAt\" IS NOT NULL AND \"PurgeAt\" > \"DeletedAt\")");
                    table.CheckConstraint("CK_attachments_owner_scope", "(\"OwnerType\" = 'Recipe' AND camp_id IS NULL AND \"QuotaScope\" = 'OrganizationRecipeLibrary') OR (\"OwnerType\" <> 'Recipe' AND camp_id IS NOT NULL AND \"QuotaScope\" = 'Camp')");
                    table.CheckConstraint("CK_attachments_size", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 10485760");
                });

            migrationBuilder.CreateTable(
                name: "read_grants",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attachment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_read_grants", x => x.Id);
                    table.CheckConstraint("CK_read_grants_expiry", "\"ExpiresAt\" > \"CreatedAt\" AND \"ExpiresAt\" <= \"CreatedAt\" + interval '60 seconds'");
                    table.CheckConstraint("CK_read_grants_hash", "octet_length(\"TokenHash\") = 32");
                    table.ForeignKey(
                        name: "FK_read_grants_attachments_attachment_id",
                        column: x => x.attachment_id,
                        principalSchema: "files",
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_BlobName",
                schema: "files",
                table: "attachments",
                column: "BlobName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachments_organization_id_camp_id_OwnerType_owner_id_State",
                schema: "files",
                table: "attachments",
                columns: new[] { "organization_id", "camp_id", "OwnerType", "owner_id", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_organization_id_camp_id_QuotaScope_State",
                schema: "files",
                table: "attachments",
                columns: new[] { "organization_id", "camp_id", "QuotaScope", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_State_PurgeAt",
                schema: "files",
                table: "attachments",
                columns: new[] { "State", "PurgeAt" });

            migrationBuilder.CreateIndex(
                name: "IX_read_grants_actor_id_ExpiresAt_UsedAt",
                schema: "files",
                table: "read_grants",
                columns: new[] { "actor_id", "ExpiresAt", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_read_grants_attachment_id",
                schema: "files",
                table: "read_grants",
                column: "attachment_id");

            migrationBuilder.CreateIndex(
                name: "IX_read_grants_TokenHash",
                schema: "files",
                table: "read_grants",
                column: "TokenHash",
                unique: true);

            migrationBuilder.Sql(
                """
                GRANT USAGE ON SCHEMA files TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON files.attachments TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON files.read_grants TO freizeit_app;

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
                        AND NOT EXISTS (
                            SELECT 1
                            FROM identity.users AS users
                            WHERE users."Id" = nullif(current_setting('app.user_id', true), '')::uuid
                              AND users."IsPlatformAdmin")
                        AND EXISTS (
                            SELECT 1
                            FROM identity.organizations AS organizations
                            JOIN identity.memberships AS memberships
                              ON memberships.organization_id = organizations."Id"
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
                                      SELECT 1
                                      FROM identity.camp_assignments AS assignments
                                      WHERE assignments.organization_id = target_organization_id
                                        AND assignments.camp_id = target_camp_id
                                        AND assignments.user_id = memberships.user_id
                                        AND assignments."IsActive"
                                        AND (NOT require_write OR assignments."Role" IN (2, 3)))
                              END);
                $function$;

                ALTER TABLE files.attachments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE files.attachments FORCE ROW LEVEL SECURITY;
                ALTER TABLE files.read_grants ENABLE ROW LEVEL SECURITY;
                ALTER TABLE files.read_grants FORCE ROW LEVEL SECURITY;

                CREATE POLICY attachments_select ON files.attachments
                    FOR SELECT TO freizeit_app
                    USING (files.runtime_can_access_scope(organization_id, camp_id, false));
                CREATE POLICY attachments_insert ON files.attachments
                    FOR INSERT TO freizeit_app
                    WITH CHECK (files.runtime_can_access_scope(organization_id, camp_id, true));
                CREATE POLICY attachments_update ON files.attachments
                    FOR UPDATE TO freizeit_app
                    USING (files.runtime_can_access_scope(organization_id, camp_id, true))
                    WITH CHECK (files.runtime_can_access_scope(organization_id, camp_id, true));
                CREATE POLICY attachments_delete_pending ON files.attachments
                    FOR DELETE TO freizeit_app
                    USING ("State" = 'PendingUpload'
                           AND files.runtime_can_access_scope(organization_id, camp_id, true));

                CREATE POLICY read_grants_select ON files.read_grants
                    FOR SELECT TO freizeit_app
                    USING (actor_id = nullif(current_setting('app.user_id', true), '')::uuid
                           AND files.runtime_can_access_scope(organization_id, camp_id, false));
                CREATE POLICY read_grants_insert ON files.read_grants
                    FOR INSERT TO freizeit_app
                    WITH CHECK (actor_id = nullif(current_setting('app.user_id', true), '')::uuid
                                AND files.runtime_can_access_scope(organization_id, camp_id, false));
                CREATE POLICY read_grants_update ON files.read_grants
                    FOR UPDATE TO freizeit_app
                    USING (actor_id = nullif(current_setting('app.user_id', true), '')::uuid
                           AND files.runtime_can_access_scope(organization_id, camp_id, false))
                    WITH CHECK (actor_id = nullif(current_setting('app.user_id', true), '')::uuid
                                AND files.runtime_can_access_scope(organization_id, camp_id, false));
                CREATE POLICY read_grants_delete ON files.read_grants
                    FOR DELETE TO freizeit_app
                    USING ((actor_id = nullif(current_setting('app.user_id', true), '')::uuid
                            AND files.runtime_can_access_scope(organization_id, camp_id, false))
                           OR files.runtime_can_access_scope(organization_id, camp_id, true));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "read_grants",
                schema: "files");

            migrationBuilder.DropTable(
                name: "attachments",
                schema: "files");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS files.runtime_can_access_scope(uuid, uuid, boolean);");
        }
    }
}

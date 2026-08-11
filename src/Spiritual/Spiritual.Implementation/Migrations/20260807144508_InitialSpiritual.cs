using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spiritual.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class InitialSpiritual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spiritual");

            migrationBuilder.CreateTable(
                name: "bible_snapshots",
                schema: "spiritual",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    devotion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TextExcerpt = table.Column<string>(type: "text", nullable: false),
                    TechnicalTranslationId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TranslationDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    License = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Attribution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetrievedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "devotions",
                schema: "spiritual",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BibleReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Translation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CoreMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MarkdownContent = table.Column<string>(type: "text", nullable: false),
                    ResponsibleUserIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    MaterialNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    schedule_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_bible_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devotions_bible_snapshots_current_bible_snapshot_id",
                        column: x => x.current_bible_snapshot_id,
                        principalSchema: "spiritual",
                        principalTable: "bible_snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bible_snapshots_organization_id_camp_id_devotion_id_Retriev~",
                schema: "spiritual",
                table: "bible_snapshots",
                columns: new[] { "organization_id", "camp_id", "devotion_id", "RetrievedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_devotions_current_bible_snapshot_id",
                schema: "spiritual",
                table: "devotions",
                column: "current_bible_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_devotions_organization_id_camp_id_DeletedAt",
                schema: "spiritual",
                table: "devotions",
                columns: new[] { "organization_id", "camp_id", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_devotions_organization_id_camp_id_schedule_entry_id",
                schema: "spiritual",
                table: "devotions",
                columns: new[] { "organization_id", "camp_id", "schedule_entry_id" });

            migrationBuilder.Sql(
                """
                GRANT USAGE ON SCHEMA spiritual TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE ON spiritual.devotions TO freizeit_app;
                GRANT SELECT, INSERT ON spiritual.bible_snapshots TO freizeit_app;

                CREATE OR REPLACE FUNCTION spiritual.runtime_can_access_camp(
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
                        AND nullif(current_setting('app.camp_id', true), '')::uuid = target_camp_id
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
                              AND (
                                  memberships."OrganizationRole" = 0
                                  OR EXISTS (
                                      SELECT 1
                                      FROM identity.camp_assignments AS assignments
                                      WHERE assignments.organization_id = target_organization_id
                                        AND assignments.camp_id = target_camp_id
                                        AND assignments.user_id = memberships.user_id
                                        AND assignments."IsActive"
                                        AND (
                                            NOT require_write
                                            OR assignments."CampRole" IN (0, 1)))));
                $function$;

                ALTER TABLE spiritual.devotions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE spiritual.devotions FORCE ROW LEVEL SECURITY;
                ALTER TABLE spiritual.bible_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE spiritual.bible_snapshots FORCE ROW LEVEL SECURITY;

                CREATE POLICY devotions_select ON spiritual.devotions
                    FOR SELECT TO freizeit_app
                    USING (spiritual.runtime_can_access_camp(organization_id, camp_id, false));
                CREATE POLICY devotions_insert ON spiritual.devotions
                    FOR INSERT TO freizeit_app
                    WITH CHECK (spiritual.runtime_can_access_camp(organization_id, camp_id, true));
                CREATE POLICY devotions_update ON spiritual.devotions
                    FOR UPDATE TO freizeit_app
                    USING (spiritual.runtime_can_access_camp(organization_id, camp_id, true))
                    WITH CHECK (spiritual.runtime_can_access_camp(organization_id, camp_id, true));

                CREATE POLICY bible_snapshots_select ON spiritual.bible_snapshots
                    FOR SELECT TO freizeit_app
                    USING (spiritual.runtime_can_access_camp(organization_id, camp_id, false));
                CREATE POLICY bible_snapshots_insert ON spiritual.bible_snapshots
                    FOR INSERT TO freizeit_app
                    WITH CHECK (spiritual.runtime_can_access_camp(organization_id, camp_id, true));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "devotions",
                schema: "spiritual");

            migrationBuilder.DropTable(
                name: "bible_snapshots",
                schema: "spiritual");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS spiritual.runtime_can_access_camp(uuid, uuid, boolean);");
        }
    }
}

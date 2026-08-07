using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowledge.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class InitialKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.CreateTable(
                name: "notes",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Markdown = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    TrashedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    trashed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    PurgeAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.Id);
                    table.UniqueConstraint("AK_notes_Id_organization_id_camp_id", x => new { x.Id, x.organization_id, x.camp_id });
                });

            migrationBuilder.CreateTable(
                name: "note_links",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTitleSnapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_note_links_notes_note_id_organization_id_camp_id",
                        columns: x => new { x.note_id, x.organization_id, x.camp_id },
                        principalSchema: "knowledge",
                        principalTable: "notes",
                        principalColumns: new[] { "Id", "organization_id", "camp_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "note_tags",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_note_tags_notes_note_id_organization_id_camp_id",
                        columns: x => new { x.note_id, x.organization_id, x.camp_id },
                        principalSchema: "knowledge",
                        principalTable: "notes",
                        principalColumns: new[] { "Id", "organization_id", "camp_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_note_links_note_id_organization_id_camp_id",
                schema: "knowledge",
                table: "note_links",
                columns: new[] { "note_id", "organization_id", "camp_id" });

            migrationBuilder.CreateIndex(
                name: "IX_note_links_note_id_TargetType_target_id",
                schema: "knowledge",
                table: "note_links",
                columns: new[] { "note_id", "TargetType", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_note_links_organization_id_camp_id",
                schema: "knowledge",
                table: "note_links",
                columns: new[] { "organization_id", "camp_id" });

            migrationBuilder.CreateIndex(
                name: "IX_note_tags_note_id_NormalizedName",
                schema: "knowledge",
                table: "note_tags",
                columns: new[] { "note_id", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_note_tags_note_id_organization_id_camp_id",
                schema: "knowledge",
                table: "note_tags",
                columns: new[] { "note_id", "organization_id", "camp_id" });

            migrationBuilder.CreateIndex(
                name: "IX_note_tags_organization_id_camp_id_NormalizedName",
                schema: "knowledge",
                table: "note_tags",
                columns: new[] { "organization_id", "camp_id", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_notes_organization_id_camp_id_State_IsPinned_UpdatedAt",
                schema: "knowledge",
                table: "notes",
                columns: new[] { "organization_id", "camp_id", "State", "IsPinned", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notes_State_PurgeAfter",
                schema: "knowledge",
                table: "notes",
                columns: new[] { "State", "PurgeAfter" });

            migrationBuilder.Sql(
                """
                GRANT USAGE ON SCHEMA knowledge TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA knowledge TO freizeit_app;

                ALTER TABLE knowledge.notes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE knowledge.notes FORCE ROW LEVEL SECURITY;
                CREATE POLICY knowledge_camp_access ON knowledge.notes
                    USING (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
                    WITH CHECK (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

                ALTER TABLE knowledge.note_tags ENABLE ROW LEVEL SECURITY;
                ALTER TABLE knowledge.note_tags FORCE ROW LEVEL SECURITY;
                CREATE POLICY knowledge_camp_access ON knowledge.note_tags
                    USING (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
                    WITH CHECK (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

                ALTER TABLE knowledge.note_links ENABLE ROW LEVEL SECURITY;
                ALTER TABLE knowledge.note_links FORCE ROW LEVEL SECURITY;
                CREATE POLICY knowledge_camp_access ON knowledge.note_links
                    USING (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
                    WITH CHECK (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA knowledge FROM freizeit_app;
                REVOKE USAGE ON SCHEMA knowledge FROM freizeit_app;
                """);

            migrationBuilder.DropTable(
                name: "note_links",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "note_tags",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "notes",
                schema: "knowledge");
        }
    }
}

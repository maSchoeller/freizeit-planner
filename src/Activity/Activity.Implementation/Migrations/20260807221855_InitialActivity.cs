using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Activity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class InitialActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "activity");

            migrationBuilder.CreateTable(
                name: "activity_events",
                schema: "activity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "search_documents",
                schema: "activity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SearchText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_organization_id_camp_id_ObjectType_object_id",
                schema: "activity",
                table: "activity_events",
                columns: new[] { "organization_id", "camp_id", "ObjectType", "object_id" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_organization_id_camp_id_Timestamp",
                schema: "activity",
                table: "activity_events",
                columns: new[] { "organization_id", "camp_id", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_search_documents_organization_id_camp_id_IsRemoved_ObjectTy~",
                schema: "activity",
                table: "search_documents",
                columns: new[] { "organization_id", "camp_id", "IsRemoved", "ObjectType" });

            migrationBuilder.CreateIndex(
                name: "IX_search_documents_organization_id_camp_id_ObjectType_object_~",
                schema: "activity",
                table: "search_documents",
                columns: new[] { "organization_id", "camp_id", "ObjectType", "object_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                GRANT USAGE ON SCHEMA activity TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA activity TO freizeit_app;

                ALTER TABLE activity.activity_events ENABLE ROW LEVEL SECURITY;
                ALTER TABLE activity.activity_events FORCE ROW LEVEL SECURITY;
                CREATE POLICY activity_camp_access ON activity.activity_events
                    USING (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
                    WITH CHECK (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

                ALTER TABLE activity.search_documents ENABLE ROW LEVEL SECURITY;
                ALTER TABLE activity.search_documents FORCE ROW LEVEL SECURITY;
                CREATE POLICY activity_camp_access ON activity.search_documents
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
                REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA activity FROM freizeit_app;
                REVOKE USAGE ON SCHEMA activity FROM freizeit_app;
                """);

            migrationBuilder.DropTable(
                name: "activity_events",
                schema: "activity");

            migrationBuilder.DropTable(
                name: "search_documents",
                schema: "activity");
        }
    }
}

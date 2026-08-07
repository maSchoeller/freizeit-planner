using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camps.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class InitialCamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "camps");

            migrationBuilder.CreateTable(
                name: "camps",
                schema: "camps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DefaultPortions = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camps", x => x.Id);
                    table.UniqueConstraint("AK_camps_organization_id_Id", x => new { x.organization_id, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "schedule_entries",
                schema: "camps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllDay = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDateExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Location = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_entries", x => x.Id);
                    table.UniqueConstraint("AK_schedule_entries_organization_id_camp_id_Id", x => new { x.organization_id, x.camp_id, x.Id });
                    table.ForeignKey(
                        name: "FK_schedule_entries_camps_organization_id_camp_id",
                        columns: x => new { x.organization_id, x.camp_id },
                        principalSchema: "camps",
                        principalTable: "camps",
                        principalColumns: new[] { "organization_id", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "schedule_responsibilities",
                schema: "camps",
                columns: table => new
                {
                    schedule_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_responsibilities", x => new { x.schedule_entry_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_schedule_responsibilities_schedule_entries_organization_id_~",
                        columns: x => new { x.organization_id, x.camp_id, x.schedule_entry_id },
                        principalSchema: "camps",
                        principalTable: "schedule_entries",
                        principalColumns: new[] { "organization_id", "camp_id", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_camps_organization_id_Slug",
                schema: "camps",
                table: "camps",
                columns: new[] { "organization_id", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_camps_organization_id_Status_StartsOn",
                schema: "camps",
                table: "camps",
                columns: new[] { "organization_id", "Status", "StartsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_entries_organization_id_camp_id_StartDate",
                schema: "camps",
                table: "schedule_entries",
                columns: new[] { "organization_id", "camp_id", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_entries_organization_id_camp_id_StartsAtUtc",
                schema: "camps",
                table: "schedule_entries",
                columns: new[] { "organization_id", "camp_id", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_responsibilities_organization_id_camp_id_schedule_~",
                schema: "camps",
                table: "schedule_responsibilities",
                columns: new[] { "organization_id", "camp_id", "schedule_entry_id" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_responsibilities_organization_id_camp_id_user_id",
                schema: "camps",
                table: "schedule_responsibilities",
                columns: new[] { "organization_id", "camp_id", "user_id" });

            migrationBuilder.Sql(
                """
                GRANT USAGE ON SCHEMA camps TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA camps TO freizeit_app;

                CREATE OR REPLACE FUNCTION camps.runtime_can_read_camp(
                    target_organization_id uuid,
                    target_camp_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity, camps
                AS $function$
                    SELECT identity.runtime_is_organization_manager(target_organization_id)
                        OR (
                            identity.runtime_can_access_organization(target_organization_id)
                            AND EXISTS (
                                SELECT 1
                                FROM identity.camp_assignments AS assignment
                                WHERE assignment.organization_id = target_organization_id
                                    AND assignment.camp_id = target_camp_id
                                    AND assignment.user_id = NULLIF(
                                        current_setting('app.user_id', true), '')::uuid
                                    AND assignment."IsActive"
                            )
                        );
                $function$;

                CREATE OR REPLACE FUNCTION camps.runtime_can_manage_camp(
                    target_organization_id uuid,
                    target_camp_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity, camps
                AS $function$
                    SELECT identity.runtime_is_organization_manager(target_organization_id)
                        OR (
                            identity.runtime_can_access_organization(target_organization_id)
                            AND EXISTS (
                                SELECT 1
                                FROM identity.camp_assignments AS assignment
                                WHERE assignment.organization_id = target_organization_id
                                    AND assignment.camp_id = target_camp_id
                                    AND assignment.user_id = NULLIF(
                                        current_setting('app.user_id', true), '')::uuid
                                    AND assignment."IsActive"
                                    AND assignment."Role" = 2
                            )
                        );
                $function$;

                CREATE OR REPLACE FUNCTION camps.runtime_can_write_schedule(
                    target_organization_id uuid,
                    target_camp_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity, camps
                AS $function$
                    SELECT identity.runtime_is_organization_manager(target_organization_id)
                        OR (
                            identity.runtime_can_access_organization(target_organization_id)
                            AND EXISTS (
                                SELECT 1
                                FROM identity.camp_assignments AS assignment
                                WHERE assignment.organization_id = target_organization_id
                                    AND assignment.camp_id = target_camp_id
                                    AND assignment.user_id = NULLIF(
                                        current_setting('app.user_id', true), '')::uuid
                                    AND assignment."IsActive"
                                    AND assignment."Role" IN (2, 3)
                            )
                        );
                $function$;

                REVOKE ALL ON FUNCTION camps.runtime_can_read_camp(uuid, uuid) FROM PUBLIC;
                REVOKE ALL ON FUNCTION camps.runtime_can_manage_camp(uuid, uuid) FROM PUBLIC;
                REVOKE ALL ON FUNCTION camps.runtime_can_write_schedule(uuid, uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION camps.runtime_can_read_camp(uuid, uuid) TO freizeit_app;
                GRANT EXECUTE ON FUNCTION camps.runtime_can_manage_camp(uuid, uuid) TO freizeit_app;
                GRANT EXECUTE ON FUNCTION camps.runtime_can_write_schedule(uuid, uuid) TO freizeit_app;

                ALTER TABLE camps.camps ENABLE ROW LEVEL SECURITY;
                ALTER TABLE camps.camps FORCE ROW LEVEL SECURITY;
                ALTER TABLE camps.schedule_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE camps.schedule_entries FORCE ROW LEVEL SECURITY;
                ALTER TABLE camps.schedule_responsibilities ENABLE ROW LEVEL SECURITY;
                ALTER TABLE camps.schedule_responsibilities FORCE ROW LEVEL SECURITY;

                CREATE POLICY camps_select ON camps.camps
                    FOR SELECT TO freizeit_app
                    USING (camps.runtime_can_read_camp(organization_id, "Id"));
                CREATE POLICY camps_insert ON camps.camps
                    FOR INSERT TO freizeit_app
                    WITH CHECK (identity.runtime_is_organization_manager(organization_id));
                CREATE POLICY camps_update ON camps.camps
                    FOR UPDATE TO freizeit_app
                    USING (camps.runtime_can_manage_camp(organization_id, "Id"))
                    WITH CHECK (camps.runtime_can_manage_camp(organization_id, "Id"));
                CREATE POLICY camps_delete ON camps.camps
                    FOR DELETE TO freizeit_app
                    USING (identity.runtime_is_organization_manager(organization_id));

                CREATE POLICY schedule_entries_select ON camps.schedule_entries
                    FOR SELECT TO freizeit_app
                    USING (camps.runtime_can_read_camp(organization_id, camp_id));
                CREATE POLICY schedule_entries_insert ON camps.schedule_entries
                    FOR INSERT TO freizeit_app
                    WITH CHECK (
                        camps.runtime_can_write_schedule(organization_id, camp_id)
                        AND EXISTS (
                            SELECT 1 FROM camps.camps AS camp
                            WHERE camp.organization_id = schedule_entries.organization_id
                                AND camp."Id" = schedule_entries.camp_id
                                AND camp."Status" = 0
                        )
                    );
                CREATE POLICY schedule_entries_update ON camps.schedule_entries
                    FOR UPDATE TO freizeit_app
                    USING (camps.runtime_can_write_schedule(organization_id, camp_id))
                    WITH CHECK (
                        camps.runtime_can_write_schedule(organization_id, camp_id)
                        AND EXISTS (
                            SELECT 1 FROM camps.camps AS camp
                            WHERE camp.organization_id = schedule_entries.organization_id
                                AND camp."Id" = schedule_entries.camp_id
                                AND camp."Status" = 0
                        )
                    );
                CREATE POLICY schedule_entries_delete ON camps.schedule_entries
                    FOR DELETE TO freizeit_app
                    USING (
                        camps.runtime_can_write_schedule(organization_id, camp_id)
                        AND EXISTS (
                            SELECT 1 FROM camps.camps AS camp
                            WHERE camp.organization_id = schedule_entries.organization_id
                                AND camp."Id" = schedule_entries.camp_id
                                AND camp."Status" = 0
                        )
                    );

                CREATE POLICY schedule_responsibilities_select ON camps.schedule_responsibilities
                    FOR SELECT TO freizeit_app
                    USING (camps.runtime_can_read_camp(organization_id, camp_id));
                CREATE POLICY schedule_responsibilities_insert ON camps.schedule_responsibilities
                    FOR INSERT TO freizeit_app
                    WITH CHECK (
                        camps.runtime_can_write_schedule(organization_id, camp_id)
                        AND EXISTS (
                            SELECT 1 FROM camps.camps AS camp
                            WHERE camp.organization_id = schedule_responsibilities.organization_id
                                AND camp."Id" = schedule_responsibilities.camp_id
                                AND camp."Status" = 0
                        )
                    );
                CREATE POLICY schedule_responsibilities_update ON camps.schedule_responsibilities
                    FOR UPDATE TO freizeit_app
                    USING (
                        camps.runtime_can_write_schedule(organization_id, camp_id)
                        AND EXISTS (
                            SELECT 1 FROM camps.camps AS camp
                            WHERE camp.organization_id = schedule_responsibilities.organization_id
                                AND camp."Id" = schedule_responsibilities.camp_id
                                AND camp."Status" = 0
                        )
                    )
                    WITH CHECK (
                        camps.runtime_can_write_schedule(organization_id, camp_id)
                        AND EXISTS (
                            SELECT 1 FROM camps.camps AS camp
                            WHERE camp.organization_id = schedule_responsibilities.organization_id
                                AND camp."Id" = schedule_responsibilities.camp_id
                                AND camp."Status" = 0
                        )
                    );
                CREATE POLICY schedule_responsibilities_delete ON camps.schedule_responsibilities
                    FOR DELETE TO freizeit_app
                    USING (
                        camps.runtime_can_write_schedule(organization_id, camp_id)
                        AND EXISTS (
                            SELECT 1 FROM camps.camps AS camp
                            WHERE camp.organization_id = schedule_responsibilities.organization_id
                                AND camp."Id" = schedule_responsibilities.camp_id
                                AND camp."Status" = 0
                        )
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS schedule_responsibilities_delete
                    ON camps.schedule_responsibilities;
                DROP POLICY IF EXISTS schedule_responsibilities_update
                    ON camps.schedule_responsibilities;
                DROP POLICY IF EXISTS schedule_responsibilities_insert
                    ON camps.schedule_responsibilities;
                DROP POLICY IF EXISTS schedule_responsibilities_select
                    ON camps.schedule_responsibilities;
                DROP POLICY IF EXISTS schedule_entries_delete ON camps.schedule_entries;
                DROP POLICY IF EXISTS schedule_entries_update ON camps.schedule_entries;
                DROP POLICY IF EXISTS schedule_entries_insert ON camps.schedule_entries;
                DROP POLICY IF EXISTS schedule_entries_select ON camps.schedule_entries;
                DROP POLICY IF EXISTS camps_delete ON camps.camps;
                DROP POLICY IF EXISTS camps_update ON camps.camps;
                DROP POLICY IF EXISTS camps_insert ON camps.camps;
                DROP POLICY IF EXISTS camps_select ON camps.camps;
                DROP FUNCTION IF EXISTS camps.runtime_can_write_schedule(uuid, uuid);
                DROP FUNCTION IF EXISTS camps.runtime_can_manage_camp(uuid, uuid);
                DROP FUNCTION IF EXISTS camps.runtime_can_read_camp(uuid, uuid);
                """);

            migrationBuilder.DropTable(
                name: "schedule_responsibilities",
                schema: "camps");

            migrationBuilder.DropTable(
                name: "schedule_entries",
                schema: "camps");

            migrationBuilder.DropTable(
                name: "camps",
                schema: "camps");
        }
    }
}

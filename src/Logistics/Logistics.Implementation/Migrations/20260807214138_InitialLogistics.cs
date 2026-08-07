using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class InitialLogistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "logistics");

            migrationBuilder.CreateTable(
                name: "material_requirements",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    QuantityValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    QuantityUnit = table.Column<int>(type: "integer", nullable: false),
                    CustomUnitName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ProcurementSource = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduleEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_requirements", x => x.Id);
                    table.UniqueConstraint("AK_material_requirements_organization_id_camp_id_Id", x => new { x.organization_id, x.camp_id, x.Id });
                    table.CheckConstraint("CK_material_custom_unit", "(\"QuantityUnit\" = 5 AND \"CustomUnitName\" IS NOT NULL) OR (\"QuantityUnit\" <> 5 AND \"CustomUnitName\" IS NULL)");
                    table.CheckConstraint("CK_material_quantity", "\"QuantityValue\" > 0");
                    table.CheckConstraint("CK_material_version", "\"Version\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "shopping_check_events",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shopping_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shopping_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResultingItemVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_check_events", x => x.Id);
                    table.CheckConstraint("CK_shopping_check_event_version", "\"ResultingItemVersion\" > 1");
                });

            migrationBuilder.CreateTable(
                name: "shopping_lists",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ChangeSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_lists", x => x.Id);
                    table.UniqueConstraint("AK_shopping_lists_organization_id_camp_id_Id", x => new { x.organization_id, x.camp_id, x.Id });
                    table.CheckConstraint("CK_shopping_lists_change_sequence", "\"ChangeSequence\" > 0");
                    table.CheckConstraint("CK_shopping_lists_version", "\"Version\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "material_responsibilities",
                schema: "logistics",
                columns: table => new
                {
                    material_requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_responsibilities", x => new { x.material_requirement_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_material_responsibilities_material_requirements_organizatio~",
                        columns: x => new { x.organization_id, x.camp_id, x.material_requirement_id },
                        principalSchema: "logistics",
                        principalTable: "material_requirements",
                        principalColumns: new[] { "organization_id", "camp_id", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shopping_items",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shopping_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuantityValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    QuantityUnit = table.Column<int>(type: "integer", nullable: false),
                    CustomUnitName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Store = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceLabel = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CateringMealId = table.Column<Guid>(type: "uuid", nullable: true),
                    CateringRecipeSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    CateringSnapshotIngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CateringSourceRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CateringSourceRecipeVersionNumber = table.Column<int>(type: "integer", nullable: true),
                    MaterialRequirementId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialRequirementVersion = table.Column<long>(type: "bigint", nullable: true),
                    IsChecked = table.Column<bool>(type: "boolean", nullable: false),
                    checked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_items", x => x.Id);
                    table.UniqueConstraint("AK_shopping_items_organization_id_camp_id_shopping_list_id_Id", x => new { x.organization_id, x.camp_id, x.shopping_list_id, x.Id });
                    table.CheckConstraint("CK_shopping_items_check_state", "(\"IsChecked\" AND \"checked_by_user_id\" IS NOT NULL AND \"CheckedAt\" IS NOT NULL) OR (NOT \"IsChecked\" AND \"checked_by_user_id\" IS NULL AND \"CheckedAt\" IS NULL)");
                    table.CheckConstraint("CK_shopping_items_custom_unit", "(\"QuantityUnit\" = 5 AND \"CustomUnitName\" IS NOT NULL) OR (\"QuantityUnit\" <> 5 AND \"CustomUnitName\" IS NULL)");
                    table.CheckConstraint("CK_shopping_items_quantity", "\"QuantityValue\" > 0");
                    table.CheckConstraint("CK_shopping_items_source", "(\"SourceKind\" = 0 AND \"CateringMealId\" IS NULL AND \"CateringRecipeSnapshotId\" IS NULL AND \"CateringSnapshotIngredientId\" IS NULL AND \"CateringSourceRecipeId\" IS NULL AND \"CateringSourceRecipeVersionNumber\" IS NULL AND \"MaterialRequirementId\" IS NULL AND \"MaterialRequirementVersion\" IS NULL) OR (\"SourceKind\" = 1 AND \"CateringMealId\" IS NOT NULL AND \"CateringRecipeSnapshotId\" IS NOT NULL AND \"CateringSnapshotIngredientId\" IS NOT NULL AND \"CateringSourceRecipeId\" IS NOT NULL AND \"CateringSourceRecipeVersionNumber\" > 0 AND \"MaterialRequirementId\" IS NULL AND \"MaterialRequirementVersion\" IS NULL) OR (\"SourceKind\" = 2 AND \"MaterialRequirementId\" IS NOT NULL AND \"MaterialRequirementVersion\" > 0 AND \"CateringMealId\" IS NULL AND \"CateringRecipeSnapshotId\" IS NULL AND \"CateringSnapshotIngredientId\" IS NULL AND \"CateringSourceRecipeId\" IS NULL AND \"CateringSourceRecipeVersionNumber\" IS NULL)");
                    table.CheckConstraint("CK_shopping_items_version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_shopping_items_shopping_lists_organization_id_camp_id_shopp~",
                        columns: x => new { x.organization_id, x.camp_id, x.shopping_list_id },
                        principalSchema: "logistics",
                        principalTable: "shopping_lists",
                        principalColumns: new[] { "organization_id", "camp_id", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shopping_item_responsibilities",
                schema: "logistics",
                columns: table => new
                {
                    shopping_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shopping_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_item_responsibilities", x => new { x.shopping_item_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_shopping_item_responsibilities_shopping_items_organization_~",
                        columns: x => new { x.organization_id, x.camp_id, x.shopping_list_id, x.shopping_item_id },
                        principalSchema: "logistics",
                        principalTable: "shopping_items",
                        principalColumns: new[] { "organization_id", "camp_id", "shopping_list_id", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_material_requirements_organization_id_camp_id_ScheduleEntry~",
                schema: "logistics",
                table: "material_requirements",
                columns: new[] { "organization_id", "camp_id", "ScheduleEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_material_requirements_organization_id_camp_id_Status",
                schema: "logistics",
                table: "material_requirements",
                columns: new[] { "organization_id", "camp_id", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_material_responsibilities_organization_id_camp_id_material_~",
                schema: "logistics",
                table: "material_responsibilities",
                columns: new[] { "organization_id", "camp_id", "material_requirement_id" });

            migrationBuilder.CreateIndex(
                name: "IX_material_responsibilities_organization_id_camp_id_user_id",
                schema: "logistics",
                table: "material_responsibilities",
                columns: new[] { "organization_id", "camp_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_check_events_organization_id_camp_id_shopping_item~",
                schema: "logistics",
                table: "shopping_check_events",
                columns: new[] { "organization_id", "camp_id", "shopping_item_id", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_item_responsibilities_organization_id_camp_id_shop~",
                schema: "logistics",
                table: "shopping_item_responsibilities",
                columns: new[] { "organization_id", "camp_id", "shopping_list_id", "shopping_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_item_responsibilities_organization_id_camp_id_user~",
                schema: "logistics",
                table: "shopping_item_responsibilities",
                columns: new[] { "organization_id", "camp_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_items_organization_id_camp_id_shopping_list_id_IsC~",
                schema: "logistics",
                table: "shopping_items",
                columns: new[] { "organization_id", "camp_id", "shopping_list_id", "IsChecked" });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_lists_organization_id_camp_id_Name",
                schema: "logistics",
                table: "shopping_lists",
                columns: new[] { "organization_id", "camp_id", "Name" });

            migrationBuilder.Sql(
                """
                GRANT USAGE ON SCHEMA logistics TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA logistics TO freizeit_app;

                CREATE OR REPLACE FUNCTION logistics.runtime_can_read_camp(
                    target_organization_id uuid,
                    target_camp_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity, logistics
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

                CREATE OR REPLACE FUNCTION logistics.runtime_can_write_camp(
                    target_organization_id uuid,
                    target_camp_id uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, identity, logistics
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

                REVOKE ALL ON FUNCTION logistics.runtime_can_read_camp(uuid, uuid) FROM PUBLIC;
                REVOKE ALL ON FUNCTION logistics.runtime_can_write_camp(uuid, uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION logistics.runtime_can_read_camp(uuid, uuid) TO freizeit_app;
                GRANT EXECUTE ON FUNCTION logistics.runtime_can_write_camp(uuid, uuid) TO freizeit_app;

                ALTER TABLE logistics.material_requirements ENABLE ROW LEVEL SECURITY;
                ALTER TABLE logistics.material_requirements FORCE ROW LEVEL SECURITY;
                ALTER TABLE logistics.material_responsibilities ENABLE ROW LEVEL SECURITY;
                ALTER TABLE logistics.material_responsibilities FORCE ROW LEVEL SECURITY;
                ALTER TABLE logistics.shopping_lists ENABLE ROW LEVEL SECURITY;
                ALTER TABLE logistics.shopping_lists FORCE ROW LEVEL SECURITY;
                ALTER TABLE logistics.shopping_items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE logistics.shopping_items FORCE ROW LEVEL SECURITY;
                ALTER TABLE logistics.shopping_item_responsibilities ENABLE ROW LEVEL SECURITY;
                ALTER TABLE logistics.shopping_item_responsibilities FORCE ROW LEVEL SECURITY;
                ALTER TABLE logistics.shopping_check_events ENABLE ROW LEVEL SECURITY;
                ALTER TABLE logistics.shopping_check_events FORCE ROW LEVEL SECURITY;

                CREATE POLICY material_requirements_select ON logistics.material_requirements
                    FOR SELECT TO freizeit_app
                    USING (logistics.runtime_can_read_camp(organization_id, camp_id));
                CREATE POLICY material_requirements_insert ON logistics.material_requirements
                    FOR INSERT TO freizeit_app
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY material_requirements_update ON logistics.material_requirements
                    FOR UPDATE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id))
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY material_requirements_delete ON logistics.material_requirements
                    FOR DELETE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id));

                CREATE POLICY material_responsibilities_select ON logistics.material_responsibilities
                    FOR SELECT TO freizeit_app
                    USING (logistics.runtime_can_read_camp(organization_id, camp_id));
                CREATE POLICY material_responsibilities_insert ON logistics.material_responsibilities
                    FOR INSERT TO freizeit_app
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY material_responsibilities_update ON logistics.material_responsibilities
                    FOR UPDATE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id))
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY material_responsibilities_delete ON logistics.material_responsibilities
                    FOR DELETE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id));

                CREATE POLICY shopping_lists_select ON logistics.shopping_lists
                    FOR SELECT TO freizeit_app
                    USING (logistics.runtime_can_read_camp(organization_id, camp_id));
                CREATE POLICY shopping_lists_insert ON logistics.shopping_lists
                    FOR INSERT TO freizeit_app
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY shopping_lists_update ON logistics.shopping_lists
                    FOR UPDATE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id))
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY shopping_lists_delete ON logistics.shopping_lists
                    FOR DELETE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id));

                CREATE POLICY shopping_items_select ON logistics.shopping_items
                    FOR SELECT TO freizeit_app
                    USING (logistics.runtime_can_read_camp(organization_id, camp_id));
                CREATE POLICY shopping_items_insert ON logistics.shopping_items
                    FOR INSERT TO freizeit_app
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY shopping_items_update ON logistics.shopping_items
                    FOR UPDATE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id))
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY shopping_items_delete ON logistics.shopping_items
                    FOR DELETE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id));

                CREATE POLICY shopping_item_responsibilities_select
                    ON logistics.shopping_item_responsibilities
                    FOR SELECT TO freizeit_app
                    USING (logistics.runtime_can_read_camp(organization_id, camp_id));
                CREATE POLICY shopping_item_responsibilities_insert
                    ON logistics.shopping_item_responsibilities
                    FOR INSERT TO freizeit_app
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY shopping_item_responsibilities_update
                    ON logistics.shopping_item_responsibilities
                    FOR UPDATE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id))
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                CREATE POLICY shopping_item_responsibilities_delete
                    ON logistics.shopping_item_responsibilities
                    FOR DELETE TO freizeit_app
                    USING (logistics.runtime_can_write_camp(organization_id, camp_id));

                CREATE POLICY shopping_check_events_select ON logistics.shopping_check_events
                    FOR SELECT TO freizeit_app
                    USING (logistics.runtime_can_read_camp(organization_id, camp_id));
                CREATE POLICY shopping_check_events_insert ON logistics.shopping_check_events
                    FOR INSERT TO freizeit_app
                    WITH CHECK (logistics.runtime_can_write_camp(organization_id, camp_id));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS shopping_check_events_insert
                    ON logistics.shopping_check_events;
                DROP POLICY IF EXISTS shopping_check_events_select
                    ON logistics.shopping_check_events;
                DROP POLICY IF EXISTS shopping_item_responsibilities_delete
                    ON logistics.shopping_item_responsibilities;
                DROP POLICY IF EXISTS shopping_item_responsibilities_update
                    ON logistics.shopping_item_responsibilities;
                DROP POLICY IF EXISTS shopping_item_responsibilities_insert
                    ON logistics.shopping_item_responsibilities;
                DROP POLICY IF EXISTS shopping_item_responsibilities_select
                    ON logistics.shopping_item_responsibilities;
                DROP POLICY IF EXISTS shopping_items_delete ON logistics.shopping_items;
                DROP POLICY IF EXISTS shopping_items_update ON logistics.shopping_items;
                DROP POLICY IF EXISTS shopping_items_insert ON logistics.shopping_items;
                DROP POLICY IF EXISTS shopping_items_select ON logistics.shopping_items;
                DROP POLICY IF EXISTS shopping_lists_delete ON logistics.shopping_lists;
                DROP POLICY IF EXISTS shopping_lists_update ON logistics.shopping_lists;
                DROP POLICY IF EXISTS shopping_lists_insert ON logistics.shopping_lists;
                DROP POLICY IF EXISTS shopping_lists_select ON logistics.shopping_lists;
                DROP POLICY IF EXISTS material_responsibilities_delete
                    ON logistics.material_responsibilities;
                DROP POLICY IF EXISTS material_responsibilities_update
                    ON logistics.material_responsibilities;
                DROP POLICY IF EXISTS material_responsibilities_insert
                    ON logistics.material_responsibilities;
                DROP POLICY IF EXISTS material_responsibilities_select
                    ON logistics.material_responsibilities;
                DROP POLICY IF EXISTS material_requirements_delete
                    ON logistics.material_requirements;
                DROP POLICY IF EXISTS material_requirements_update
                    ON logistics.material_requirements;
                DROP POLICY IF EXISTS material_requirements_insert
                    ON logistics.material_requirements;
                DROP POLICY IF EXISTS material_requirements_select
                    ON logistics.material_requirements;
                DROP FUNCTION IF EXISTS logistics.runtime_can_write_camp(uuid, uuid);
                DROP FUNCTION IF EXISTS logistics.runtime_can_read_camp(uuid, uuid);
                """);

            migrationBuilder.DropTable(
                name: "material_responsibilities",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "shopping_check_events",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "shopping_item_responsibilities",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "material_requirements",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "shopping_items",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "shopping_lists",
                schema: "logistics");
        }
    }
}

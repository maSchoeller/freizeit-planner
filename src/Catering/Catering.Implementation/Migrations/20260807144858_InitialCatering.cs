using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catering.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catering");

            migrationBuilder.CreateTable(
                name: "ingredients",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    merged_into_ingredient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "meals",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PortionOverride = table.Column<int>(type: "integer", nullable: true),
                    schedule_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recipe_snapshots",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRecipeVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Preparation = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    BasePortions = table.Column<int>(type: "integer", nullable: false),
                    DietaryTags = table.Column<string[]>(type: "text[]", nullable: false),
                    AllergenNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    KitchenNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_snapshots_meals_meal_id",
                        column: x => x.meal_id,
                        principalSchema: "catering",
                        principalTable: "meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_versions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Preparation = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    BasePortions = table.Column<int>(type: "integer", nullable: false),
                    DietaryTags = table.Column<string[]>(type: "text[]", nullable: false),
                    AllergenNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    KitchenNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_versions_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalSchema: "catering",
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_ingredients",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountUnitName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_snapshot_ingredients_recipe_snapshots_recipe_snapshot_id",
                        column: x => x.recipe_snapshot_id,
                        principalSchema: "catering",
                        principalTable: "recipe_snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredients",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountUnitName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_recipe_versions_recipe_version_id",
                        column: x => x.recipe_version_id,
                        principalSchema: "catering",
                        principalTable: "recipe_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ingredients_organization_id_NormalizedName",
                schema: "catering",
                table: "ingredients",
                columns: new[] { "organization_id", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meals_organization_id_camp_id",
                schema: "catering",
                table: "meals",
                columns: new[] { "organization_id", "camp_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_organization_id",
                schema: "catering",
                table: "recipe_ingredients",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_recipe_version_id",
                schema: "catering",
                table: "recipe_ingredients",
                column: "recipe_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_snapshots_meal_id_IsCurrent",
                schema: "catering",
                table: "recipe_snapshots",
                columns: new[] { "meal_id", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_snapshots_organization_id_camp_id",
                schema: "catering",
                table: "recipe_snapshots",
                columns: new[] { "organization_id", "camp_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_versions_organization_id",
                schema: "catering",
                table: "recipe_versions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_versions_recipe_id_Number",
                schema: "catering",
                table: "recipe_versions",
                columns: new[] { "recipe_id", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recipes_organization_id",
                schema: "catering",
                table: "recipes",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_ingredients_organization_id_camp_id",
                schema: "catering",
                table: "snapshot_ingredients",
                columns: new[] { "organization_id", "camp_id" });

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_ingredients_recipe_snapshot_id",
                schema: "catering",
                table: "snapshot_ingredients",
                column: "recipe_snapshot_id");

            migrationBuilder.Sql(
                """
                GRANT USAGE ON SCHEMA catering TO freizeit_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA catering TO freizeit_app;

                ALTER TABLE catering.ingredients ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catering.ingredients FORCE ROW LEVEL SECURITY;
                CREATE POLICY catering_organization_access ON catering.ingredients
                    USING (identity.runtime_can_access_organization(organization_id))
                    WITH CHECK (identity.runtime_can_access_organization(organization_id));

                ALTER TABLE catering.recipes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catering.recipes FORCE ROW LEVEL SECURITY;
                CREATE POLICY catering_organization_access ON catering.recipes
                    USING (identity.runtime_can_access_organization(organization_id))
                    WITH CHECK (identity.runtime_can_access_organization(organization_id));

                ALTER TABLE catering.recipe_versions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catering.recipe_versions FORCE ROW LEVEL SECURITY;
                CREATE POLICY catering_organization_access ON catering.recipe_versions
                    USING (identity.runtime_can_access_organization(organization_id))
                    WITH CHECK (identity.runtime_can_access_organization(organization_id));

                ALTER TABLE catering.recipe_ingredients ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catering.recipe_ingredients FORCE ROW LEVEL SECURITY;
                CREATE POLICY catering_organization_access ON catering.recipe_ingredients
                    USING (identity.runtime_can_access_organization(organization_id))
                    WITH CHECK (identity.runtime_can_access_organization(organization_id));

                ALTER TABLE catering.meals ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catering.meals FORCE ROW LEVEL SECURITY;
                CREATE POLICY catering_camp_access ON catering.meals
                    USING (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
                    WITH CHECK (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

                ALTER TABLE catering.recipe_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catering.recipe_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY catering_camp_access ON catering.recipe_snapshots
                    USING (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid)
                    WITH CHECK (
                        identity.runtime_can_access_organization(organization_id)
                        AND camp_id = nullif(current_setting('app.camp_id', true), '')::uuid);

                ALTER TABLE catering.snapshot_ingredients ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catering.snapshot_ingredients FORCE ROW LEVEL SECURITY;
                CREATE POLICY catering_camp_access ON catering.snapshot_ingredients
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
                REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA catering FROM freizeit_app;
                REVOKE USAGE ON SCHEMA catering FROM freizeit_app;
                """);

            migrationBuilder.DropTable(
                name: "ingredients",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "recipe_ingredients",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "snapshot_ingredients",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "recipe_versions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "recipe_snapshots",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "recipes",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "meals",
                schema: "catering");
        }
    }
}

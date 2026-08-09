using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "logistics",
                table: "material_requirements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgeAt",
                schema: "logistics",
                table: "material_requirements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_requirements_organization_id_camp_id_PurgeAt",
                schema: "logistics",
                table: "material_requirements",
                columns: new[] { "organization_id", "camp_id", "PurgeAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_material_requirements_organization_id_camp_id_PurgeAt",
                schema: "logistics",
                table: "material_requirements");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "logistics",
                table: "material_requirements");

            migrationBuilder.DropColumn(
                name: "PurgeAt",
                schema: "logistics",
                table: "material_requirements");
        }
    }
}

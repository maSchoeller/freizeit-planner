using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "logistics",
                table: "shopping_lists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgeAt",
                schema: "logistics",
                table: "shopping_lists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_shopping_lists_organization_id_camp_id_PurgeAt",
                schema: "logistics",
                table: "shopping_lists",
                columns: new[] { "organization_id", "camp_id", "PurgeAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shopping_lists_organization_id_camp_id_PurgeAt",
                schema: "logistics",
                table: "shopping_lists");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "logistics",
                table: "shopping_lists");

            migrationBuilder.DropColumn(
                name: "PurgeAt",
                schema: "logistics",
                table: "shopping_lists");
        }
    }
}

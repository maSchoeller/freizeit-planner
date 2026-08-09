using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catering.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddMealTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "catering",
                table: "meals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgeAt",
                schema: "catering",
                table: "meals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_meals_organization_id_camp_id_PurgeAt",
                schema: "catering",
                table: "meals",
                columns: new[] { "organization_id", "camp_id", "PurgeAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_meals_organization_id_camp_id_PurgeAt",
                schema: "catering",
                table: "meals");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "catering",
                table: "meals");

            migrationBuilder.DropColumn(
                name: "PurgeAt",
                schema: "catering",
                table: "meals");
        }
    }
}

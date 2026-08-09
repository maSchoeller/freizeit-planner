using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camps.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleEntryTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "camps",
                table: "schedule_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgeAt",
                schema: "camps",
                table: "schedule_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_entries_organization_id_camp_id_PurgeAt",
                schema: "camps",
                table: "schedule_entries",
                columns: new[] { "organization_id", "camp_id", "PurgeAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_schedule_entries_organization_id_camp_id_PurgeAt",
                schema: "camps",
                table: "schedule_entries");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "camps",
                table: "schedule_entries");

            migrationBuilder.DropColumn(
                name: "PurgeAt",
                schema: "camps",
                table: "schedule_entries");
        }
    }
}

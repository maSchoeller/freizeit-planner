using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camps.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddCampsDataConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_schedule_entries_timing",
                schema: "camps",
                table: "schedule_entries",
                sql: "(\"IsAllDay\" AND \"StartsAtUtc\" IS NULL AND \"EndsAtUtc\" IS NULL AND \"StartDate\" IS NOT NULL AND \"EndDateExclusive\" > \"StartDate\") OR (NOT \"IsAllDay\" AND \"StartsAtUtc\" IS NOT NULL AND \"EndsAtUtc\" > \"StartsAtUtc\" AND \"StartDate\" IS NULL AND \"EndDateExclusive\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_schedule_entries_version",
                schema: "camps",
                table: "schedule_entries",
                sql: "\"Version\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_camps_dates",
                schema: "camps",
                table: "camps",
                sql: "\"EndsOn\" >= \"StartsOn\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_camps_default_portions",
                schema: "camps",
                table: "camps",
                sql: "\"DefaultPortions\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_camps_version",
                schema: "camps",
                table: "camps",
                sql: "\"Version\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_schedule_entries_timing",
                schema: "camps",
                table: "schedule_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_schedule_entries_version",
                schema: "camps",
                table: "schedule_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_camps_dates",
                schema: "camps",
                table: "camps");

            migrationBuilder.DropCheckConstraint(
                name: "CK_camps_default_portions",
                schema: "camps",
                table: "camps");

            migrationBuilder.DropCheckConstraint(
                name: "CK_camps_version",
                schema: "camps",
                table: "camps");
        }
    }
}

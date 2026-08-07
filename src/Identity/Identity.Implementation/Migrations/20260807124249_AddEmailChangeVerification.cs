using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailChangeVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_change_challenges",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_change_challenges", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_change_challenges_ExpiresAt",
                schema: "identity",
                table: "email_change_challenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_email_change_challenges_NormalizedEmail",
                schema: "identity",
                table: "email_change_challenges",
                column: "NormalizedEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_change_challenges",
                schema: "identity");
        }
    }
}

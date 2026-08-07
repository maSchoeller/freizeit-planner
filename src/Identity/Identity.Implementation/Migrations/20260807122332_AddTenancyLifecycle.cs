using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddTenancyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "camp_assignments",
                schema: "identity",
                columns: table => new
                {
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_assignments", x => new { x.camp_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsPlatformInvitation = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RotatedFromId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "identity",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => new { x.organization_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletionScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_camp_assignments_organization_id_user_id",
                schema: "identity",
                table: "camp_assignments",
                columns: new[] { "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invitations_ExpiresAt",
                schema: "identity",
                table: "invitations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_organization_id_NormalizedEmail",
                schema: "identity",
                table: "invitations",
                columns: new[] { "organization_id", "NormalizedEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_user_id_IsActive",
                schema: "identity",
                table: "memberships",
                columns: new[] { "user_id", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                schema: "identity",
                table: "organizations",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "camp_assignments",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "invitations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                schema: "identity",
                table: "users");
        }
    }
}

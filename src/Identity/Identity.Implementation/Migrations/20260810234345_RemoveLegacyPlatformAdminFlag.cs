using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyPlatformAdminFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE identity.memberships
                SET "Role" = 1
                WHERE "Role" = 0;

                UPDATE identity.invitations
                SET "RevokedAt" = CURRENT_TIMESTAMP,
                    "Version" = "Version" + 1
                WHERE "RevokedAt" IS NULL AND "UsedAt" IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                schema: "identity",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}

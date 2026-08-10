using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferableInvitationLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transferable_invitations",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsSuperAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationRole = table.Column<int>(type: "integer", nullable: true),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    CampRole = table.Column<int>(type: "integer", nullable: true),
                    NewOrganizationName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    NewOrganizationSlug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReservedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RotatedFromId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferable_invitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transferable_invitations_ExpiresAt",
                schema: "identity",
                table: "transferable_invitations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_transferable_invitations_TokenHash",
                schema: "identity",
                table: "transferable_invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.Sql(
                "GRANT SELECT, INSERT, UPDATE, DELETE ON identity.transferable_invitations TO freizeit_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transferable_invitations",
                schema: "identity");
        }
    }
}

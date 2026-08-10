using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReservedByUserId",
                schema: "identity",
                table: "transferable_invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "invitation_registrations",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invitation_registrations_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_registrations_invitation_id_user_id_ExpiresAt",
                schema: "identity",
                table: "invitation_registrations",
                columns: new[] { "invitation_id", "user_id", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_registrations_TokenHash",
                schema: "identity",
                table: "invitation_registrations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitation_registrations_user_id",
                schema: "identity",
                table: "invitation_registrations",
                column: "user_id");

            migrationBuilder.Sql(
                "GRANT SELECT, INSERT, UPDATE, DELETE ON identity.invitation_registrations TO freizeit_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitation_registrations",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "ReservedByUserId",
                schema: "identity",
                table: "transferable_invitations");
        }
    }
}

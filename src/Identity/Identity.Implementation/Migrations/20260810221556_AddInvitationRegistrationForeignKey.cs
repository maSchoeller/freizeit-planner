using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationRegistrationForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_invitation_registrations_transferable_invitations_invitatio~",
                schema: "identity",
                table: "invitation_registrations",
                column: "invitation_id",
                principalSchema: "identity",
                principalTable: "transferable_invitations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invitation_registrations_transferable_invitations_invitatio~",
                schema: "identity",
                table: "invitation_registrations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePasswordlessAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "users",
                type: "character varying(161)",
                maxLength: 161,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "identity",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "identity",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "identity",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReauthenticatedAt",
                schema: "identity",
                table: "login_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenHash",
                schema: "identity",
                table: "login_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RememberMe",
                schema: "identity",
                table: "login_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "identity",
                table: "login_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE identity.users
                SET "IsSuperAdmin" = "IsPlatformAdmin", "Version" = 1;

                DELETE FROM identity.login_sessions;

                UPDATE identity.invitations
                SET "RevokedAt" = CURRENT_TIMESTAMP, "Version" = "Version" + 1
                WHERE "RevokedAt" IS NULL AND "UsedAt" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ReauthenticatedAt",
                schema: "identity",
                table: "login_sessions");

            migrationBuilder.DropColumn(
                name: "RefreshTokenHash",
                schema: "identity",
                table: "login_sessions");

            migrationBuilder.DropColumn(
                name: "RememberMe",
                schema: "identity",
                table: "login_sessions");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "identity",
                table: "login_sessions");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(161)",
                oldMaxLength: 161);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "users",
                column: "NormalizedEmail");
        }
    }
}

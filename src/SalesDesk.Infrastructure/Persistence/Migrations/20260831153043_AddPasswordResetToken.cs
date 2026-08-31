using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "password_reset_token_expires_at_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_reset_token_hash",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_password_reset_token_hash",
                table: "users",
                column: "password_reset_token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_password_reset_token_hash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_reset_token_expires_at_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_reset_token_hash",
                table: "users");
        }
    }
}

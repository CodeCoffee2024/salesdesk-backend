using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionUpgradeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_upgrade_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approval_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_upgrade_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_upgrade_requests_approval_token_hash",
                table: "subscription_upgrade_requests",
                column: "approval_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscription_upgrade_requests_workspace_id",
                table: "subscription_upgrade_requests",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_upgrade_requests");
        }
    }
}

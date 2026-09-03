using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGCashPaymentSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "g_cash_payment_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount_php = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    g_cash_reference_number = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    sender_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sender_mobile_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    screenshot_data_url = table.Column<string>(type: "text", nullable: true),
                    approval_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_g_cash_payment_submissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_g_cash_payment_submissions_approval_token_hash",
                table: "g_cash_payment_submissions",
                column: "approval_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_g_cash_payment_submissions_workspace_id",
                table: "g_cash_payment_submissions",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "g_cash_payment_submissions");
        }
    }
}

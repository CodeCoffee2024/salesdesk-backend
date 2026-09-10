using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentPaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "paid_amount",
                table: "documents",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "paid_at_utc",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unpaid");

            migrationBuilder.AddColumn<string>(
                name: "stripe_checkout_session_id",
                table: "documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_stripe_checkout_session_id",
                table: "documents",
                column: "stripe_checkout_session_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_stripe_checkout_session_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "paid_amount",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "paid_at_utc",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "stripe_checkout_session_id",
                table: "documents");
        }
    }
}

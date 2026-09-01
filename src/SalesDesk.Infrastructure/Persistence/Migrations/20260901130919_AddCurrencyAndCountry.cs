using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyAndCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows get a sensible real default (US/USD), not an empty
            // string, so a pre-TASK-029 workspace/document doesn't end up with a
            // blank ISO code that Intl.NumberFormat would reject on the frontend.
            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "workspaces",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "US");

            migrationBuilder.AddColumn<string>(
                name: "default_currency",
                table: "workspaces",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "client_country",
                table: "documents",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "documents",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "customers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "country",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "default_currency",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "client_country",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "country",
                table: "customers");
        }
    }
}

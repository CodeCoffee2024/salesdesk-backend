using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPromo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_early_bird_promo",
                table: "workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "subscription_end_date",
                table: "workspaces",
                type: "timestamp with time zone",
                nullable: true);

            // Existing rows default to "Free" (not ""), matching SubscriptionTier's
            // string conversion — an empty string isn't a valid enum member and
            // would throw the moment EF tried to read a pre-TASK-031 workspace back.
            migrationBuilder.AddColumn<string>(
                name: "subscription_tier",
                table: "workspaces",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Free");

            migrationBuilder.CreateTable(
                name: "promo_counters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promo_counters", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "promo_counters",
                columns: new[] { "id", "count", "key" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), 0, "early_bird_premium" });

            migrationBuilder.CreateIndex(
                name: "ix_promo_counters_key",
                table: "promo_counters",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promo_counters");

            migrationBuilder.DropColumn(
                name: "is_early_bird_promo",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "subscription_end_date",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "subscription_tier",
                table: "workspaces");
        }
    }
}

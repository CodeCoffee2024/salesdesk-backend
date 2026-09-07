using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurring_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    client_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    due_date_offset_days = table.Column<int>(type: "integer", nullable: false),
                    interval = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    next_run_date = table.Column<DateOnly>(type: "date", nullable: false),
                    auto_dispatch = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_schedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_schedules_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_schedules_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_schedule_line_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurring_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_schedule_line_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_schedule_line_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_recurring_schedule_line_items_recurring_schedules_recurring",
                        column: x => x.recurring_schedule_id,
                        principalTable: "recurring_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_schedule_line_items_product_id",
                table: "recurring_schedule_line_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_schedule_line_items_recurring_schedule_id",
                table: "recurring_schedule_line_items",
                column: "recurring_schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_schedules_customer_id",
                table: "recurring_schedules",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_schedules_is_active_next_run_date",
                table: "recurring_schedules",
                columns: new[] { "is_active", "next_run_date" });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_schedules_template_id",
                table: "recurring_schedules",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_schedules_workspace_id",
                table: "recurring_schedules",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurring_schedule_line_items");

            migrationBuilder.DropTable(
                name: "recurring_schedules");
        }
    }
}

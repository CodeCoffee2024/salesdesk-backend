using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_reminder_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_reminder_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_reminder_logs_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reminder_settings_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    quote_follow_up_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    invoice_due_warning_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    overdue_notices_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    cc_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminder_settings_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_reminder_settings_entries_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_reminder_logs_document_id_type",
                table: "document_reminder_logs",
                columns: new[] { "document_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminder_settings_entries_workspace_id",
                table: "reminder_settings_entries",
                column: "workspace_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_reminder_logs");

            migrationBuilder.DropTable(
                name: "reminder_settings_entries");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_document_number",
                table: "documents");

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "templates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "customers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_templates_workspace_id",
                table: "templates",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_workspace_id",
                table: "products",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_workspace_id",
                table: "documents",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_workspace_id_document_number",
                table: "documents",
                columns: new[] { "workspace_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_workspace_id",
                table: "customers",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_templates_workspace_id",
                table: "templates");

            migrationBuilder.DropIndex(
                name: "ix_products_workspace_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_documents_workspace_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_documents_workspace_id_document_number",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_customers_workspace_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "templates");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "customers");

            migrationBuilder.CreateIndex(
                name: "ix_documents_document_number",
                table: "documents",
                column: "document_number",
                unique: true);
        }
    }
}

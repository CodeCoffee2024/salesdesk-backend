using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added nullable first, backfilled with a distinct token per existing row,
            // then tightened to NOT NULL below — a single shared default (or
            // Guid.Empty) would collide with the unique index this migration also
            // adds the moment more than one document already exists.
            migrationBuilder.AddColumn<Guid>(
                name: "public_token",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE documents SET public_token = gen_random_uuid() WHERE public_token IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "public_token",
                table: "documents",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "document_signatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    signer_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    signature_image_data_url = table.Column<string>(type: "text", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    signed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    document_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_signatures", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_signatures_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documents_public_token",
                table: "documents",
                column: "public_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_signatures_document_id",
                table: "document_signatures",
                column: "document_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_signatures");

            migrationBuilder.DropIndex(
                name: "ix_documents_public_token",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "public_token",
                table: "documents");
        }
    }
}

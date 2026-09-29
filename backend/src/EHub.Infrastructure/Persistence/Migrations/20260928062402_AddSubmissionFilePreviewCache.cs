using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionFilePreviewCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "preview_generated_at",
                table: "submission_files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_pdf_public_id",
                table: "submission_files",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_pdf_url",
                table: "submission_files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preview_source_version_number",
                table: "submission_files",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preview_generated_at",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "preview_pdf_public_id",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "preview_pdf_url",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "preview_source_version_number",
                table: "submission_files");
        }
    }
}

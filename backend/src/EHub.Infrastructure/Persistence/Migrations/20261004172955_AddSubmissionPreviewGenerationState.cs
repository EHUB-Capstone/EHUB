using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionPreviewGenerationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "preview_attempt_count",
                table: "submission_files",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "preview_last_error",
                table: "submission_files",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "preview_next_attempt_at_utc",
                table: "submission_files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_status",
                table: "submission_files",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.CreateIndex(
                name: "IX_submission_files_preview_status_preview_next_attempt_at_utc",
                table: "submission_files",
                columns: new[] { "preview_status", "preview_next_attempt_at_utc" });

            // Backfill: files whose PDF preview is already cached for their current version are Ready.
            // Everything else stays None and is converted on first request (or queued by the upload flow).
            migrationBuilder.Sql(@"
                UPDATE submission_files
                SET preview_status = 'Ready'
                WHERE preview_source_version_number = version_number
                  AND (preview_pdf_url IS NOT NULL OR preview_pdf_public_id IS NOT NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submission_files_preview_status_preview_next_attempt_at_utc",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "preview_attempt_count",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "preview_last_error",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "preview_next_attempt_at_utc",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "preview_status",
                table: "submission_files");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSubmissionFileVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submission_files_submission_id",
                table: "submission_files");

            migrationBuilder.AddColumn<int>(
                name: "version_number",
                table: "submission_files",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Older uploads can share one Submission.VersionNumber. Assign file versions
            // across the team project/checkpoint in upload order without moving files or
            // changing feedback, requirements, evaluations, or their submission links.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT sf.id,
                           ROW_NUMBER() OVER (
                               PARTITION BY s.project_id, s.checkpoint_id
                               ORDER BY sf.uploaded_at, s.version_number, sf.id
                           )::integer AS file_version
                    FROM submission_files AS sf
                    JOIN submissions AS s ON s.id = sf.submission_id
                )
                UPDATE submission_files AS sf
                SET version_number = ranked.file_version
                FROM ranked
                WHERE sf.id = ranked.id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_submission_files_submission_id_version_number",
                table: "submission_files",
                columns: new[] { "submission_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submission_files_submission_id_version_number",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "version_number",
                table: "submission_files");

            migrationBuilder.CreateIndex(
                name: "IX_submission_files_submission_id",
                table: "submission_files",
                column: "submission_id");
        }
    }
}

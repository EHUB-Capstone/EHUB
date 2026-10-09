using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionUploadSessionsAndR2Storage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "storage_key",
                table: "submission_files",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_provider",
                table: "submission_files",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cloudinary");

            migrationBuilder.CreateTable(
                name: "submission_upload_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    original_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    declared_size = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submission_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_upload_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_submission_upload_sessions_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_submission_upload_sessions_object_key",
                table: "submission_upload_sessions",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_upload_sessions_status_expires_at_utc",
                table: "submission_upload_sessions",
                columns: new[] { "status", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_submission_upload_sessions_team_id",
                table: "submission_upload_sessions",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_upload_sessions_user_id_status",
                table: "submission_upload_sessions",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_upload_sessions");

            migrationBuilder.DropColumn(
                name: "storage_key",
                table: "submission_files");

            migrationBuilder.DropColumn(
                name: "storage_provider",
                table: "submission_files");
        }
    }
}

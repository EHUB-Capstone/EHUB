using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionRequirementContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submission_requirement_contents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_requirement_contents", x => x.id);
                    table.ForeignKey(
                        name: "FK_submission_requirement_contents_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_submission_requirement_contents_submission_id_requirement_i~",
                table: "submission_requirement_contents",
                columns: new[] { "submission_id", "requirement_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_requirement_contents");
        }
    }
}

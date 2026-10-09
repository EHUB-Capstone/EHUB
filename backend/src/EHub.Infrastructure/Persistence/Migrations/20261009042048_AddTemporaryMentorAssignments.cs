using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTemporaryMentorAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "temporary_mentor_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    slot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_temporary_mentor_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_temporary_mentor_assignments_mentor_import_drafts_draft_id",
                        column: x => x.draft_id,
                        principalTable: "mentor_import_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_temporary_mentor_assignments_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_temporary_mentor_assignments_draft_id",
                table: "temporary_mentor_assignments",
                column: "draft_id");

            migrationBuilder.CreateIndex(
                name: "IX_temporary_mentor_assignments_team_id_slot",
                table: "temporary_mentor_assignments",
                columns: new[] { "team_id", "slot" },
                unique: true,
                filter: "status = 'Active' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_temporary_mentor_assignments_team_id_slot_status",
                table: "temporary_mentor_assignments",
                columns: new[] { "team_id", "slot", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "temporary_mentor_assignments");
        }
    }
}

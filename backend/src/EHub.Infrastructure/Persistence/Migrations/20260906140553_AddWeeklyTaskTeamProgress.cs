using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyTaskTeamProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "weekly_task_team_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekly_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    checklist_json = table.Column<string>(type: "jsonb", nullable: true),
                    completion_percentage = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_weekly_task_team_progress", x => x.id);
                    table.CheckConstraint("CK_WeeklyTaskTeamProgress_CompletionPercentageRange", "completion_percentage >= 0 AND completion_percentage <= 100");
                    table.ForeignKey(
                        name: "FK_weekly_task_team_progress_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_weekly_task_team_progress_weekly_tasks_weekly_task_id",
                        column: x => x.weekly_task_id,
                        principalTable: "weekly_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_task_team_progress_team_id",
                table: "weekly_task_team_progress",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_task_team_progress_weekly_task_id_team_id",
                table: "weekly_task_team_progress",
                columns: new[] { "weekly_task_id", "team_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "weekly_task_team_progress");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamProjectContinuity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "previous_team_id",
                table: "teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "team_lineage_id",
                table: "teams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "previous_project_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "project_lineage_id",
                table: "projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill: every existing team/project becomes the first term of its own lineage.
            migrationBuilder.Sql("UPDATE teams SET team_lineage_id = id;");
            migrationBuilder.Sql("UPDATE projects SET project_lineage_id = id;");

            migrationBuilder.CreateTable(
                name: "team_continuations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_lineage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dissolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dissolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_continuations", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_continuations_classes_target_class_id",
                        column: x => x.target_class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_continuations_semesters_target_semester_id",
                        column: x => x.target_semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_continuations_teams_created_team_id",
                        column: x => x.created_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_team_continuations_teams_source_team_id",
                        column: x => x.source_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "team_continuation_members",
                columns: table => new
                {
                    continuation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_continuation_members", x => new { x.continuation_id, x.student_id });
                    table.ForeignKey(
                        name: "FK_team_continuation_members_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_continuation_members_team_continuations_continuation_id",
                        column: x => x.continuation_id,
                        principalTable: "team_continuations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_teams_previous_team_id",
                table: "teams",
                column: "previous_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_teams_team_lineage_id",
                table: "teams",
                column: "team_lineage_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_previous_project_id",
                table: "projects",
                column: "previous_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_project_lineage_id",
                table: "projects",
                column: "project_lineage_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_continuation_members_student_id",
                table: "team_continuation_members",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_continuations_created_team_id",
                table: "team_continuations",
                column: "created_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_continuations_source_team_id",
                table: "team_continuations",
                column: "source_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_continuations_target_class_id",
                table: "team_continuations",
                column: "target_class_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_continuations_target_semester_id",
                table: "team_continuations",
                column: "target_semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_continuations_team_lineage_id_target_semester_id",
                table: "team_continuations",
                columns: new[] { "team_lineage_id", "target_semester_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_projects_projects_previous_project_id",
                table: "projects",
                column: "previous_project_id",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_teams_teams_previous_team_id",
                table: "teams",
                column: "previous_team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_projects_projects_previous_project_id",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "FK_teams_teams_previous_team_id",
                table: "teams");

            migrationBuilder.DropTable(
                name: "team_continuation_members");

            migrationBuilder.DropTable(
                name: "team_continuations");

            migrationBuilder.DropIndex(
                name: "IX_teams_previous_team_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_teams_team_lineage_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_projects_previous_project_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_project_lineage_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "previous_team_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "team_lineage_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "previous_project_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "project_lineage_id",
                table: "projects");
        }
    }
}

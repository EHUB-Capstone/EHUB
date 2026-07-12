using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mentor_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expertise = table.Column<string[]>(type: "text[]", nullable: false),
                    bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    linkedin_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    max_teams = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_mentor_profiles", x => x.id);
                    table.CheckConstraint("CK_MentorProfile_MaxTeams", "max_teams >= 0");
                    table.ForeignKey(
                        name: "FK_mentor_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mentor_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_mentor_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_mentor_assignments_mentor_profiles_mentor_profile_id",
                        column: x => x.mentor_profile_id,
                        principalTable: "mentor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentor_assignments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentor_assignments_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentor_assignments_users_assigned_by_id",
                        column: x => x.assigned_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mentoring_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentor_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    meeting_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_mentoring_sessions", x => x.id);
                    table.CheckConstraint("CK_MentoringSession_EndAtAfterStartAt", "end_at > start_at");
                    table.ForeignKey(
                        name: "FK_mentoring_sessions_mentor_assignments_mentor_assignment_id",
                        column: x => x.mentor_assignment_id,
                        principalTable: "mentor_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentoring_sessions_users_lecturer_user_id",
                        column: x => x.lecturer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mentoring_action_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentoring_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_mentoring_action_items", x => x.id);
                    table.CheckConstraint("CK_MentoringActionItem_SingleAssignee", "NOT (assignee_user_id IS NOT NULL AND assignee_student_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_mentoring_action_items_mentoring_sessions_mentoring_session~",
                        column: x => x.mentoring_session_id,
                        principalTable: "mentoring_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentoring_action_items_students_assignee_student_id",
                        column: x => x.assignee_student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentoring_action_items_users_assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mentoring_attendances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentoring_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    attended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    check_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_mentoring_attendances", x => x.id);
                    table.CheckConstraint("CK_MentoringAttendance_HasIdentification", "user_id IS NOT NULL OR student_id IS NOT NULL OR name IS NOT NULL OR email IS NOT NULL");
                    table.CheckConstraint("CK_MentoringAttendance_SingleParticipantType", "NOT (user_id IS NOT NULL AND student_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_mentoring_attendances_mentoring_sessions_mentoring_session_~",
                        column: x => x.mentoring_session_id,
                        principalTable: "mentoring_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentoring_attendances_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentoring_attendances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_assigned_at",
                table: "mentor_assignments",
                column: "assigned_at");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_assigned_by_id",
                table: "mentor_assignments",
                column: "assigned_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_mentor_profile_id",
                table: "mentor_assignments",
                column: "mentor_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_mentor_profile_id_team_id_status",
                table: "mentor_assignments",
                columns: new[] { "mentor_profile_id", "team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_project_id",
                table: "mentor_assignments",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_status",
                table: "mentor_assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id_status",
                table: "mentor_assignments",
                columns: new[] { "team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_profiles_organization",
                table: "mentor_profiles",
                column: "organization");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_profiles_status",
                table: "mentor_profiles",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_profiles_user_id",
                table: "mentor_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_action_items_assignee_student_id",
                table: "mentoring_action_items",
                column: "assignee_student_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_action_items_assignee_user_id",
                table: "mentoring_action_items",
                column: "assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_action_items_completed",
                table: "mentoring_action_items",
                column: "completed");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_action_items_due_date",
                table: "mentoring_action_items",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_action_items_mentoring_session_id",
                table: "mentoring_action_items",
                column: "mentoring_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_attendances_attended",
                table: "mentoring_attendances",
                column: "attended");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_attendances_mentoring_session_id",
                table: "mentoring_attendances",
                column: "mentoring_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_attendances_mentoring_session_id_student_id",
                table: "mentoring_attendances",
                columns: new[] { "mentoring_session_id", "student_id" },
                unique: true,
                filter: "student_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_attendances_mentoring_session_id_user_id",
                table: "mentoring_attendances",
                columns: new[] { "mentoring_session_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_attendances_student_id",
                table: "mentoring_attendances",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_attendances_user_id",
                table: "mentoring_attendances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_sessions_lecturer_user_id",
                table: "mentoring_sessions",
                column: "lecturer_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_sessions_mentor_assignment_id",
                table: "mentoring_sessions",
                column: "mentor_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_sessions_mentor_assignment_id_start_at",
                table: "mentoring_sessions",
                columns: new[] { "mentor_assignment_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_sessions_start_at",
                table: "mentoring_sessions",
                column: "start_at");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_sessions_status",
                table: "mentoring_sessions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentoring_action_items");

            migrationBuilder.DropTable(
                name: "mentoring_attendances");

            migrationBuilder.DropTable(
                name: "mentoring_sessions");

            migrationBuilder.DropTable(
                name: "mentor_assignments");

            migrationBuilder.DropTable(
                name: "mentor_profiles");
        }
    }
}

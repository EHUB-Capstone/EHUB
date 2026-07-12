using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEcosystemExtensionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "milestones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_milestones", x => x.id);
                    table.CheckConstraint("CK_Milestone_DueDateAfterOrEqualsStartDate", "due_date >= start_date");
                    table.CheckConstraint("CK_Milestone_ProgressRange", "progress >= 0 AND progress <= 100");
                    table.ForeignKey(
                        name: "FK_milestones_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_milestones_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_milestones_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_milestones_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_milestones_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    strengths_json = table.Column<string>(type: "jsonb", nullable: false),
                    weaknesses_json = table.Column<string>(type: "jsonb", nullable: false),
                    feasibility_analysis = table.Column<string>(type: "text", nullable: true),
                    market_potential = table.Column<string>(type: "text", nullable: true),
                    risks_json = table.Column<string>(type: "jsonb", nullable: false),
                    similar_ideas_json = table.Column<string>(type: "jsonb", nullable: false),
                    suggestions_json = table.Column<string>(type: "jsonb", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    generated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_project_analyses", x => x.id);
                    table.CheckConstraint("CK_ProjectAnalysis_ScoreRange", "score IS NULL OR (score >= 0 AND score <= 100)");
                    table.ForeignKey(
                        name: "FK_project_analyses_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_analyses_users_generated_by_id",
                        column: x => x.generated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weekly_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    task_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    week_number = table.Column<int>(type: "integer", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: true),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    priority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attachments_json = table.Column<string>(type: "jsonb", nullable: false),
                    checklist_json = table.Column<string>(type: "jsonb", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    is_template = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    visible_to_students = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    completion_percentage = table.Column<int>(type: "integer", nullable: false),
                    estimated_hours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_weekly_tasks", x => x.id);
                    table.CheckConstraint("CK_WeeklyTask_CompletionPercentageRange", "completion_percentage >= 0 AND completion_percentage <= 100");
                    table.CheckConstraint("CK_WeeklyTask_EstimatedHoursNonNegative", "estimated_hours IS NULL OR estimated_hours >= 0");
                    table.CheckConstraint("CK_WeeklyTask_WeekNumberPositive", "week_number >= 1");
                    table.ForeignKey(
                        name: "FK_weekly_tasks_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weekly_tasks_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weekly_tasks_students_assignee_student_id",
                        column: x => x.assignee_student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weekly_tasks_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weekly_tasks_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weekly_tasks_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workshops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_audience = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    format = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    banner_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    attachments_json = table.Column<string>(type: "jsonb", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    meeting_link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_workshops", x => x.id);
                    table.CheckConstraint("CK_Workshop_EndAtAfterStartAt", "end_at > start_at");
                    table.ForeignKey(
                        name: "FK_workshops_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sprint_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    priority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_sprint_tasks", x => x.id);
                    table.CheckConstraint("CK_SprintTask_PositionNonNegative", "position >= 0");
                    table.CheckConstraint("CK_SprintTask_SingleAssigneeType", "NOT (assignee_user_id IS NOT NULL AND assignee_student_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_sprint_tasks_milestones_milestone_id",
                        column: x => x.milestone_id,
                        principalTable: "milestones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sprint_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sprint_tasks_students_assignee_student_id",
                        column: x => x.assignee_student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sprint_tasks_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sprint_tasks_users_assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sprint_tasks_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sprint_tasks_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workshop_attendances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workshop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    evidence_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    check_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reject_reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_workshop_attendances", x => x.id);
                    table.CheckConstraint("CK_WorkshopAttendance_HasIdentification", "NOT (user_id IS NULL AND student_id IS NULL)");
                    table.CheckConstraint("CK_WorkshopAttendance_SingleParticipantType", "NOT (user_id IS NOT NULL AND student_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_workshop_attendances_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_attendances_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_attendances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_attendances_users_verified_by_id",
                        column: x => x.verified_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_attendances_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_milestones_class_id",
                table: "milestones",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_milestones_created_by_id",
                table: "milestones",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_milestones_due_date",
                table: "milestones",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "IX_milestones_project_id",
                table: "milestones",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_milestones_project_id_status",
                table: "milestones",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_milestones_status",
                table: "milestones",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_milestones_team_id",
                table: "milestones",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_milestones_team_id_status",
                table: "milestones",
                columns: new[] { "team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_milestones_updated_by_id",
                table: "milestones",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_analyses_analysis_type",
                table: "project_analyses",
                column: "analysis_type");

            migrationBuilder.CreateIndex(
                name: "IX_project_analyses_generated_at",
                table: "project_analyses",
                column: "generated_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_analyses_generated_by_id",
                table: "project_analyses",
                column: "generated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_analyses_project_id",
                table: "project_analyses",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_analyses_project_id_generated_at",
                table: "project_analyses",
                columns: new[] { "project_id", "generated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_project_analyses_score",
                table: "project_analyses",
                column: "score");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_assignee_student_id",
                table: "sprint_tasks",
                column: "assignee_student_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_assignee_user_id",
                table: "sprint_tasks",
                column: "assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_created_by_id",
                table: "sprint_tasks",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_due_date",
                table: "sprint_tasks",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_milestone_id",
                table: "sprint_tasks",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_milestone_id_position",
                table: "sprint_tasks",
                columns: new[] { "milestone_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_position",
                table: "sprint_tasks",
                column: "position");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_priority",
                table: "sprint_tasks",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_project_id",
                table: "sprint_tasks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_status",
                table: "sprint_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_team_id",
                table: "sprint_tasks",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_team_id_status",
                table: "sprint_tasks",
                columns: new[] { "team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_updated_by_id",
                table: "sprint_tasks",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_assignee_student_id",
                table: "weekly_tasks",
                column: "assignee_student_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_class_id",
                table: "weekly_tasks",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_class_id_week_number",
                table: "weekly_tasks",
                columns: new[] { "class_id", "week_number" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_course_id",
                table: "weekly_tasks",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_course_id_week_number",
                table: "weekly_tasks",
                columns: new[] { "course_id", "week_number" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_created_by_id",
                table: "weekly_tasks",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_is_template",
                table: "weekly_tasks",
                column: "is_template");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_priority",
                table: "weekly_tasks",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_scope",
                table: "weekly_tasks",
                column: "scope");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_status",
                table: "weekly_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_team_id",
                table: "weekly_tasks",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_team_id_week_number",
                table: "weekly_tasks",
                columns: new[] { "team_id", "week_number" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_updated_by_id",
                table: "weekly_tasks",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_tasks_week_number",
                table: "weekly_tasks",
                column: "week_number");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_attendances_class_id",
                table: "workshop_attendances",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_attendances_status",
                table: "workshop_attendances",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_attendances_student_id",
                table: "workshop_attendances",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_attendances_user_id",
                table: "workshop_attendances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_attendances_verified_by_id",
                table: "workshop_attendances",
                column: "verified_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_attendances_workshop_id",
                table: "workshop_attendances",
                column: "workshop_id");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_attendances_workshop_id_student_id",
                table: "workshop_attendances",
                columns: new[] { "workshop_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workshop_attendances_workshop_id_user_id",
                table: "workshop_attendances",
                columns: new[] { "workshop_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workshops_created_by_id",
                table: "workshops",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_workshops_format",
                table: "workshops",
                column: "format");

            migrationBuilder.CreateIndex(
                name: "IX_workshops_start_at",
                table: "workshops",
                column: "start_at");

            migrationBuilder.CreateIndex(
                name: "IX_workshops_start_at_end_at",
                table: "workshops",
                columns: new[] { "start_at", "end_at" });

            migrationBuilder.CreateIndex(
                name: "IX_workshops_status",
                table: "workshops",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_workshops_status_start_at",
                table: "workshops",
                columns: new[] { "status", "start_at" });

            migrationBuilder.CreateIndex(
                name: "IX_workshops_target_audience",
                table: "workshops",
                column: "target_audience");

            migrationBuilder.CreateIndex(
                name: "IX_workshops_type",
                table: "workshops",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_analyses");

            migrationBuilder.DropTable(
                name: "sprint_tasks");

            migrationBuilder.DropTable(
                name: "weekly_tasks");

            migrationBuilder.DropTable(
                name: "workshop_attendances");

            migrationBuilder.DropTable(
                name: "milestones");

            migrationBuilder.DropTable(
                name: "workshops");
        }
    }
}

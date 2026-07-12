using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rubrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    course_id = table.Column<Guid>(type: "uuid", nullable: true),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    checkpoint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_weight = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_rubrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_rubrics_checkpoints_checkpoint_id",
                        column: x => x.checkpoint_id,
                        principalTable: "checkpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rubrics_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rubrics_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rubrics_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_score = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    max_total_score = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    overall_feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    strengths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    weaknesses = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    suggestions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluations_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluations_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluations_users_evaluator_id",
                        column: x => x.evaluator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_rubric_criteria", x => x.id);
                    table.ForeignKey(
                        name: "FK_rubric_criteria_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    changed_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_histories_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluation_histories_users_changed_by_id",
                        column: x => x.changed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_criterion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_evaluation_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_details_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluation_details_rubric_criteria_rubric_criterion_id",
                        column: x => x.rubric_criterion_id,
                        principalTable: "rubric_criteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_details_evaluation_id_rubric_criterion_id",
                table: "evaluation_details",
                columns: new[] { "evaluation_id", "rubric_criterion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_details_rubric_criterion_id",
                table: "evaluation_details",
                column: "rubric_criterion_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_histories_changed_by_id",
                table: "evaluation_histories",
                column: "changed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_histories_evaluation_id_version",
                table: "evaluation_histories",
                columns: new[] { "evaluation_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluations_evaluator_id",
                table: "evaluations",
                column: "evaluator_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluations_project_id",
                table: "evaluations",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluations_rubric_id",
                table: "evaluations",
                column: "rubric_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluations_submission_id_rubric_id_evaluator_id",
                table: "evaluations",
                columns: new[] { "submission_id", "rubric_id", "evaluator_id" },
                unique: true,
                filter: "submission_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rubric_criteria_rubric_id_display_order",
                table: "rubric_criteria",
                columns: new[] { "rubric_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rubric_criteria_rubric_id_name",
                table: "rubric_criteria",
                columns: new[] { "rubric_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_checkpoint_id",
                table: "rubrics",
                column: "checkpoint_id");

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_class_id_checkpoint_id_name",
                table: "rubrics",
                columns: new[] { "class_id", "checkpoint_id", "name" },
                unique: true,
                filter: "class_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_course_id_checkpoint_id_name",
                table: "rubrics",
                columns: new[] { "course_id", "checkpoint_id", "name" },
                unique: true,
                filter: "course_id IS NOT NULL AND class_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_created_by_id",
                table: "rubrics",
                column: "created_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_details");

            migrationBuilder.DropTable(
                name: "evaluation_histories");

            migrationBuilder.DropTable(
                name: "rubric_criteria");

            migrationBuilder.DropTable(
                name: "evaluations");

            migrationBuilder.DropTable(
                name: "rubrics");
        }
    }
}

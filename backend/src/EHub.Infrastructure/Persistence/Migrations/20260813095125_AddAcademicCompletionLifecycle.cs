using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicCompletionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at_utc",
                table: "semesters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completed_by_user_id",
                table: "semesters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completion_reason",
                table: "semesters",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "semesters",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at_utc",
                table: "classes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completed_by_user_id",
                table: "classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completion_reason",
                table: "classes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at_utc",
                table: "class_students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completed_by_user_id",
                table: "class_students",
                type: "uuid",
                nullable: true);

            // Normalize legacy rows before enforcing the new lifecycle invariants.
            migrationBuilder.Sql(
                """
                UPDATE class_students
                SET counts_toward_course_semester_limit = CASE
                        WHEN enrollment_status = 'Dropped' THEN false
                        ELSE true
                    END,
                    completed_at_utc = CASE
                        WHEN enrollment_status = 'Completed' THEN COALESCE(updated_at, created_at, NOW())
                        ELSE NULL
                    END;

                UPDATE semesters
                SET completed_at_utc = COALESCE(
                        (end_date::timestamp AT TIME ZONE 'UTC'),
                        updated_at,
                        created_at,
                        NOW()),
                    completion_reason = COALESCE(completion_reason, 'Migrated from legacy completed semester')
                WHERE status = 'Completed' AND completed_at_utc IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "semester_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    details_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semester_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_semester_audit_logs_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_semester_audit_logs_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_semesters_completed_by_user_id",
                table: "semesters",
                column: "completed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_semesters_status",
                table: "semesters",
                column: "status",
                unique: true,
                filter: "status = 'Active' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_semesters_term_year",
                table: "semesters",
                columns: new[] { "term", "year" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_semesters_completion_metadata",
                table: "semesters",
                sql: "status <> 'Completed' OR (completed_at_utc IS NOT NULL AND completion_reason IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_semesters_date_range",
                table: "semesters",
                sql: "start_date IS NULL OR end_date IS NULL OR start_date <= end_date");

            migrationBuilder.CreateIndex(
                name: "IX_classes_completed_by_user_id",
                table: "classes",
                column: "completed_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_classes_completion_metadata",
                table: "classes",
                sql: "status <> 'Completed' OR (completed_at_utc IS NOT NULL AND completion_reason IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_class_students_completed_by_user_id",
                table: "class_students",
                column: "completed_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_students_completion_metadata",
                table: "class_students",
                sql: "(enrollment_status = 'Completed' AND completed_at_utc IS NOT NULL) OR (enrollment_status <> 'Completed' AND completed_at_utc IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_students_status_counting",
                table: "class_students",
                sql: "(enrollment_status = 'Dropped' AND counts_toward_course_semester_limit = false) OR (enrollment_status IN ('Active', 'Completed') AND counts_toward_course_semester_limit = true)");

            migrationBuilder.CreateIndex(
                name: "IX_semester_audit_logs_performed_by_user_id",
                table: "semester_audit_logs",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_semester_audit_logs_semester_id_occurred_at_utc",
                table: "semester_audit_logs",
                columns: new[] { "semester_id", "occurred_at_utc" });

            migrationBuilder.AddForeignKey(
                name: "FK_class_students_users_completed_by_user_id",
                table: "class_students",
                column: "completed_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_classes_users_completed_by_user_id",
                table: "classes",
                column: "completed_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_semesters_users_completed_by_user_id",
                table: "semesters",
                column: "completed_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_class_students_users_completed_by_user_id",
                table: "class_students");

            migrationBuilder.DropForeignKey(
                name: "FK_classes_users_completed_by_user_id",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "FK_semesters_users_completed_by_user_id",
                table: "semesters");

            migrationBuilder.DropTable(
                name: "semester_audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_semesters_completed_by_user_id",
                table: "semesters");

            migrationBuilder.DropIndex(
                name: "IX_semesters_status",
                table: "semesters");

            migrationBuilder.DropIndex(
                name: "IX_semesters_term_year",
                table: "semesters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_semesters_completion_metadata",
                table: "semesters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_semesters_date_range",
                table: "semesters");

            migrationBuilder.DropIndex(
                name: "IX_classes_completed_by_user_id",
                table: "classes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_classes_completion_metadata",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_class_students_completed_by_user_id",
                table: "class_students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_class_students_completion_metadata",
                table: "class_students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_class_students_status_counting",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "completed_at_utc",
                table: "semesters");

            migrationBuilder.DropColumn(
                name: "completed_by_user_id",
                table: "semesters");

            migrationBuilder.DropColumn(
                name: "completion_reason",
                table: "semesters");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "semesters");

            migrationBuilder.DropColumn(
                name: "completed_at_utc",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "completed_by_user_id",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "completion_reason",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "completed_at_utc",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "completed_by_user_id",
                table: "class_students");
        }
    }
}

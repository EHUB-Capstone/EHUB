using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassDomainFoundationPhase05 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_class_students_student_id",
                table: "class_students");

            migrationBuilder.DropIndex(
                name: "IX_class_lecturers_class_id",
                table: "class_lecturers");

            migrationBuilder.AddColumn<bool>(
                name: "counts_toward_course_semester_limit",
                table: "class_students",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "course_id",
                table: "class_students",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "major_code_at_enrollment",
                table: "class_students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "major_verification_status",
                table: "class_students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "major_verified_at_utc",
                table: "class_students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "major_verified_by_user_id",
                table: "class_students",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "semester_id",
                table: "class_students",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill immutable enrollment scope and enrollment-major snapshots.
            // UNDECLARED preserves unknown legacy data without inventing a major.
            migrationBuilder.Sql(
                """
                UPDATE class_students AS cs
                SET semester_id = c.semester_id,
                    course_id = c.course_id,
                    major_code_at_enrollment = COALESCE(NULLIF(UPPER(TRIM(s.major_code)), ''), 'UNDECLARED'),
                    major_verification_status = 'Unverified',
                    counts_toward_course_semester_limit = (cs.enrollment_status <> 'Dropped' AND c.status <> 'Archived')
                FROM classes AS c, students AS s
                WHERE c.id = cs.class_id
                  AND s.id = cs.student_id;
                """);

            // Keep enrollment history but exempt legacy duplicates from the new
            // uniqueness constraint. The earliest non-dropped enrollment remains authoritative.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT class_id,
                           student_id,
                           ROW_NUMBER() OVER (
                               PARTITION BY student_id, semester_id, course_id
                               ORDER BY created_at, class_id
                           ) AS row_number
                    FROM class_students
                    WHERE counts_toward_course_semester_limit = TRUE
                )
                UPDATE class_students AS cs
                SET counts_toward_course_semester_limit = FALSE
                FROM ranked AS r
                WHERE cs.class_id = r.class_id
                  AND cs.student_id = r.student_id
                  AND r.row_number > 1;
                """);

            // The agreed model has one lecturer only. Retain the class primary
            // lecturer and remove legacy co-lecturer rows before adding the index.
            migrationBuilder.Sql(
                """
                DELETE FROM class_lecturers AS cl
                USING classes AS c
                WHERE c.id = cl.class_id
                  AND (c.primary_lecturer_id IS NULL OR cl.lecturer_id <> c.primary_lecturer_id);

                UPDATE class_lecturers SET is_primary = TRUE;
                """);

            // Existing incomplete classes become Draft instead of violating the
            // Active invariant. No class or schedule data is deleted.
            migrationBuilder.Sql(
                """
                UPDATE classes
                SET status = 'Draft'
                WHERE status = 'Active'
                  AND (
                      primary_lecturer_id IS NULL OR
                      schedule_json IS NULL OR
                      jsonb_typeof(schedule_json) <> 'array' OR
                      jsonb_array_length(schedule_json) = 0
                  );
                """);

            migrationBuilder.CreateTable(
                name: "class_import_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_rows_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_import_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_import_sessions_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_class_import_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_classes_active_requires_lecturer_and_schedule",
                table: "classes",
                sql: "status <> 'Active' OR (primary_lecturer_id IS NOT NULL AND schedule_json IS NOT NULL AND jsonb_typeof(schedule_json) = 'array' AND jsonb_array_length(schedule_json) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_class_students_major_verified_by_user_id",
                table: "class_students",
                column: "major_verified_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_students_student_id_semester_id_course_id",
                table: "class_students",
                columns: new[] { "student_id", "semester_id", "course_id" },
                unique: true,
                filter: "counts_toward_course_semester_limit = true");

            migrationBuilder.CreateIndex(
                name: "IX_class_lecturers_class_id",
                table: "class_lecturers",
                column: "class_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_lecturers_primary_only",
                table: "class_lecturers",
                sql: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "IX_class_import_sessions_class_id_user_id_status",
                table: "class_import_sessions",
                columns: new[] { "class_id", "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_class_import_sessions_expires_at_utc",
                table: "class_import_sessions",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_class_import_sessions_user_id",
                table: "class_import_sessions",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_class_students_users_major_verified_by_user_id",
                table: "class_students",
                column: "major_verified_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_class_students_users_major_verified_by_user_id",
                table: "class_students");

            migrationBuilder.DropTable(
                name: "class_import_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_classes_active_requires_lecturer_and_schedule",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_class_students_major_verified_by_user_id",
                table: "class_students");

            migrationBuilder.DropIndex(
                name: "IX_class_students_student_id_semester_id_course_id",
                table: "class_students");

            migrationBuilder.DropIndex(
                name: "IX_class_lecturers_class_id",
                table: "class_lecturers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_class_lecturers_primary_only",
                table: "class_lecturers");

            migrationBuilder.DropColumn(
                name: "counts_toward_course_semester_limit",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "course_id",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "major_code_at_enrollment",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "major_verification_status",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "major_verified_at_utc",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "major_verified_by_user_id",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "semester_id",
                table: "class_students");

            migrationBuilder.CreateIndex(
                name: "IX_class_students_student_id",
                table: "class_students",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_lecturers_class_id",
                table: "class_lecturers",
                column: "class_id",
                unique: true,
                filter: "is_primary = true");
        }
    }
}

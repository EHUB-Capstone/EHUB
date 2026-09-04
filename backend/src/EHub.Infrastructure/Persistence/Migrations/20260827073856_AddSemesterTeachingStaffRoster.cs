using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSemesterTeachingStaffRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "semester_staff_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_semester_staff_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_semester_staff_assignments_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_semester_staff_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_semester_staff_assignments_semester_id_role_status",
                table: "semester_staff_assignments",
                columns: new[] { "semester_id", "role", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_semester_staff_assignments_semester_id_user_id_role",
                table: "semester_staff_assignments",
                columns: new[] { "semester_id", "user_id", "role" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_semester_staff_assignments_user_id",
                table: "semester_staff_assignments",
                column: "user_id");

            // Preserve the pre-roster behavior for semesters that are still open:
            // every active Lecturer/Mentor account starts as available and Admin can
            // then deactivate entries independently for each semester.
            migrationBuilder.Sql(
                """
                INSERT INTO semester_staff_assignments
                    (id, semester_id, user_id, role, status, created_at, is_deleted)
                SELECT
                    md5(s.id::text || ':' || u.id::text || ':' || r.name)::uuid,
                    s.id,
                    u.id,
                    r.name,
                    'Active',
                    NOW(),
                    false
                FROM semesters s
                CROSS JOIN user_roles ur
                INNER JOIN roles r ON r.id = ur.role_id
                INNER JOIN users u ON u.id = ur.user_id
                WHERE s.is_deleted = false
                  AND s.status IN ('Planned', 'Active')
                  AND u.is_deleted = false
                  AND u.status = 'Active'
                  AND r.name IN ('Lecturer', 'Mentor')
                ON CONFLICT DO NOTHING;
                """);

            // Keep historical staff visible even when their semester is already
            // completed or archived.
            migrationBuilder.Sql(
                """
                INSERT INTO semester_staff_assignments
                    (id, semester_id, user_id, role, status, created_at, is_deleted)
                SELECT DISTINCT
                    md5(c.semester_id::text || ':' || cl.lecturer_id::text || ':Lecturer')::uuid,
                    c.semester_id,
                    cl.lecturer_id,
                    'Lecturer',
                    'Active',
                    NOW(),
                    false
                FROM class_lecturers cl
                INNER JOIN classes c ON c.id = cl.class_id
                WHERE c.is_deleted = false
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO semester_staff_assignments
                    (id, semester_id, user_id, role, status, created_at, is_deleted)
                SELECT DISTINCT
                    md5(c.semester_id::text || ':' || mp.user_id::text || ':Mentor')::uuid,
                    c.semester_id,
                    mp.user_id,
                    'Mentor',
                    'Active',
                    NOW(),
                    false
                FROM mentor_assignments ma
                INNER JOIN mentor_profiles mp ON mp.id = ma.mentor_profile_id
                INNER JOIN teams t ON t.id = ma.team_id
                INNER JOIN classes c ON c.id = t.class_id
                WHERE c.is_deleted = false
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "semester_staff_assignments");
        }
    }
}

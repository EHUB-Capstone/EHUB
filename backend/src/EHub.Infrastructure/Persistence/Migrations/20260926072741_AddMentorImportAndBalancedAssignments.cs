using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorImportAndBalancedAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MentorProfile_MaxTeams",
                table: "mentor_profiles");

            migrationBuilder.DropIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments");

            migrationBuilder.DropIndex(
                name: "IX_mentor_assignments_team_id_status",
                table: "mentor_assignments");

            migrationBuilder.DropColumn(
                name: "max_teams",
                table: "mentor_profiles");

            migrationBuilder.AddColumn<string>(
                name: "contract_type",
                table: "mentor_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "current_address",
                table: "mentor_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "mentor_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "mentor_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "education_level",
                table: "mentor_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fpt_email",
                table: "mentor_profiles",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "job_title",
                table: "mentor_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mentor_type",
                table: "mentor_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Enterprise");

            migrationBuilder.AddColumn<string>(
                name: "slot",
                table: "mentor_assignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Enterprise");

            migrationBuilder.CreateTable(
                name: "mentor_allocation_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    rows_json = table.Column<string>(type: "jsonb", nullable: false),
                    seed = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentor_allocation_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_mentor_allocation_sessions_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mentor_allocation_sessions_users_admin_user_id",
                        column: x => x.admin_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mentor_import_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rows_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentor_import_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_mentor_import_sessions_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mentor_import_sessions_users_admin_user_id",
                        column: x => x.admin_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_profiles_mentor_type",
                table: "mentor_profiles",
                column: "mentor_type");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id_slot",
                table: "mentor_assignments",
                columns: new[] { "team_id", "slot" },
                unique: true,
                filter: "status = 'Active' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id_slot_status",
                table: "mentor_assignments",
                columns: new[] { "team_id", "slot", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_allocation_sessions_admin_user_id_status",
                table: "mentor_allocation_sessions",
                columns: new[] { "admin_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_allocation_sessions_expires_at_utc",
                table: "mentor_allocation_sessions",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_allocation_sessions_semester_id",
                table: "mentor_allocation_sessions",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_import_sessions_admin_user_id_status",
                table: "mentor_import_sessions",
                columns: new[] { "admin_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_import_sessions_expires_at_utc",
                table: "mentor_import_sessions",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_import_sessions_semester_id",
                table: "mentor_import_sessions",
                column: "semester_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentor_allocation_sessions");

            migrationBuilder.DropTable(
                name: "mentor_import_sessions");

            migrationBuilder.DropIndex(
                name: "IX_mentor_profiles_mentor_type",
                table: "mentor_profiles");

            migrationBuilder.DropIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments");

            migrationBuilder.DropIndex(
                name: "IX_mentor_assignments_team_id_slot",
                table: "mentor_assignments");

            migrationBuilder.DropIndex(
                name: "IX_mentor_assignments_team_id_slot_status",
                table: "mentor_assignments");

            // The previous schema supports only one active mentor per team. Preserve the
            // enterprise slot and close academic assignments before restoring that constraint.
            migrationBuilder.Sql(
                """
                UPDATE mentor_assignments
                SET status = 'Ended',
                    ended_at = COALESCE(ended_at, CURRENT_TIMESTAMP),
                    updated_at = CURRENT_TIMESTAMP
                WHERE slot = 'Academic'
                  AND status = 'Active'
                  AND is_deleted = false;
                """);

            migrationBuilder.DropColumn(
                name: "contract_type",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "current_address",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "department",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "education_level",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "fpt_email",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "job_title",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "mentor_type",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "slot",
                table: "mentor_assignments");

            migrationBuilder.AddColumn<int>(
                name: "max_teams",
                table: "mentor_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MentorProfile_MaxTeams",
                table: "mentor_profiles",
                sql: "max_teams >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments",
                column: "team_id",
                unique: true,
                filter: "status = 'Active' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id_status",
                table: "mentor_assignments",
                columns: new[] { "team_id", "status" });
        }
    }
}

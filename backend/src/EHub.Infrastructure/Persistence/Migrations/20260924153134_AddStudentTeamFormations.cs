using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentTeamFormations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "team_formations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creator_student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_leader_student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    normalized_team_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    completed_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_team_formations", x => x.id);
                    table.CheckConstraint("CK_team_formations_completed_team", "(status = 'Completed') = (completed_team_id IS NOT NULL)");
                    table.CheckConstraint("CK_team_formations_status", "status IN ('Pending', 'Completed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_team_formations_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_formations_students_creator_student_id",
                        column: x => x.creator_student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_formations_students_proposed_leader_student_id",
                        column: x => x.proposed_leader_student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_formations_teams_completed_team_id",
                        column: x => x.completed_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_formation_invitations",
                columns: table => new
                {
                    formation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    responded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reservation_released_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_formation_invitations", x => new { x.formation_id, x.student_id });
                    table.CheckConstraint("CK_team_formation_invitations_status", "status IN ('Pending', 'Accepted', 'Declined')");
                    table.ForeignKey(
                        name: "FK_team_formation_invitations_class_students_class_id_student_~",
                        columns: x => new { x.class_id, x.student_id },
                        principalTable: "class_students",
                        principalColumns: new[] { "class_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_formation_invitations_team_formations_formation_id",
                        column: x => x.formation_id,
                        principalTable: "team_formations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_formation_invitations_class_id_student_id",
                table: "team_formation_invitations",
                columns: new[] { "class_id", "student_id" },
                unique: true,
                filter: "reservation_released_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_team_formation_invitations_student_id_reservation_released_~",
                table: "team_formation_invitations",
                columns: new[] { "student_id", "reservation_released_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_team_formations_class_id_normalized_team_name",
                table: "team_formations",
                columns: new[] { "class_id", "normalized_team_name" },
                unique: true,
                filter: "status = 'Pending' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_team_formations_class_id_status",
                table: "team_formations",
                columns: new[] { "class_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_team_formations_completed_team_id",
                table: "team_formations",
                column: "completed_team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_formations_creator_student_id_status",
                table: "team_formations",
                columns: new[] { "creator_student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_team_formations_proposed_leader_student_id",
                table: "team_formations",
                column: "proposed_leader_student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_formation_invitations");

            migrationBuilder.DropTable(
                name: "team_formations");
        }
    }
}

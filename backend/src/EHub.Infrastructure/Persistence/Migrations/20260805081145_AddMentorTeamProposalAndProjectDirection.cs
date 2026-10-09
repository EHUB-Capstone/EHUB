using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorTeamProposalAndProjectDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teams_students_leader_id",
                table: "teams");

            migrationBuilder.DropForeignKey(
                name: "FK_teams_users_mentor_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_teams_leader_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_teams_mentor_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_team_members_class_id_student_id",
                table: "team_members");

            migrationBuilder.DropIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments");

            // Normalize legacy duplicate leaders before TeamMember becomes the only leader source.
            migrationBuilder.Sql("""
                WITH ranked_members AS (
                    SELECT tm.team_id,
                           tm.student_id,
                           ROW_NUMBER() OVER (
                               PARTITION BY tm.team_id
                               ORDER BY CASE
                                   WHEN t.leader_id = tm.student_id THEN 0
                                   WHEN tm.role_in_team = 'Leader' THEN 1
                                   ELSE 2
                               END,
                               tm.joined_at,
                               tm.student_id
                           ) AS leader_rank
                    FROM team_members tm
                    INNER JOIN teams t ON t.id = tm.team_id
                )
                UPDATE team_members tm
                SET role_in_team = CASE WHEN ranked.leader_rank = 1 THEN 'Leader' ELSE 'Member' END
                FROM ranked_members ranked
                WHERE tm.team_id = ranked.team_id AND tm.student_id = ranked.student_id;
                """);

            // Preserve an existing MentorAssignment as the source of truth. Only backfill the
            // legacy Team.mentor_id value when the team has no current active assignment.
            migrationBuilder.Sql("""
                INSERT INTO mentor_assignments (
                    id, mentor_profile_id, team_id, project_id, assigned_by_id,
                    assigned_at, ended_at, status, note, created_at, created_by,
                    updated_at, updated_by, is_deleted, deleted_at, deleted_by)
                SELECT md5(t.id::text || mp.id::text || 'legacy-team-mentor')::uuid,
                       mp.id,
                       t.id,
                       NULL,
                       COALESCE(t.created_by_id, t.created_by, t.mentor_id),
                       COALESCE(t.created_at, NOW()),
                       NULL,
                       'Active',
                       'Migrated from legacy teams.mentor_id',
                       NOW(),
                       COALESCE(t.created_by_id, t.created_by, t.mentor_id),
                       NULL, NULL, FALSE, NULL, NULL
                FROM teams t
                INNER JOIN mentor_profiles mp ON mp.user_id = t.mentor_id
                WHERE t.mentor_id IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM mentor_assignments ma
                      WHERE ma.team_id = t.id AND ma.status = 'Active' AND ma.is_deleted = FALSE
                  );
                """);

            // If legacy data contains multiple active assignments, retain the newest and close
            // the others before creating the filtered unique index.
            migrationBuilder.Sql("""
                WITH ranked_assignments AS (
                    SELECT id,
                           ROW_NUMBER() OVER (PARTITION BY team_id ORDER BY assigned_at DESC, id) AS assignment_rank
                    FROM mentor_assignments
                    WHERE status = 'Active' AND is_deleted = FALSE
                )
                UPDATE mentor_assignments ma
                SET status = 'Ended', ended_at = COALESCE(ma.ended_at, NOW()), updated_at = NOW()
                FROM ranked_assignments ranked
                WHERE ma.id = ranked.id AND ranked.assignment_rank > 1;
                """);

            migrationBuilder.DropColumn(
                name: "leader_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "mentor_id",
                table: "teams");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "teams",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "teams",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<bool>(
                name: "counts_toward_active_team",
                table: "team_members",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "project_directions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_project_directions", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_directions_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_directions_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_by_student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    project_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    latest_review_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    approved_team_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_team_proposals", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_proposals_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_proposals_students_proposed_by_student_id",
                        column: x => x.proposed_by_student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_proposals_teams_approved_team_id",
                        column: x => x.approved_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_proposals_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_direction_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_direction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_direction_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_direction_reviews_project_directions_project_direct~",
                        column: x => x.project_direction_id,
                        principalTable: "project_directions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_direction_reviews_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_proposal_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_proposal_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_proposal_history_team_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "team_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_proposal_history_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_proposal_members",
                columns: table => new
                {
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_leader = table.Column<bool>(type: "boolean", nullable: false),
                    is_included = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    counts_toward_open_proposal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_proposal_members", x => new { x.proposal_id, x.student_id });
                    table.ForeignKey(
                        name: "FK_team_proposal_members_class_students_class_id_student_id",
                        columns: x => new { x.class_id, x.student_id },
                        principalTable: "class_students",
                        principalColumns: new[] { "class_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_proposal_members_team_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "team_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_members_class_id_student_id",
                table: "team_members",
                columns: new[] { "class_id", "student_id" },
                unique: true,
                filter: "counts_toward_active_team = true");

            migrationBuilder.CreateIndex(
                name: "IX_team_members_team_id",
                table: "team_members",
                column: "team_id",
                unique: true,
                filter: "role_in_team = 'Leader' AND counts_toward_active_team = true");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments",
                column: "team_id",
                unique: true,
                filter: "status = 'Active' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_project_direction_reviews_project_direction_id_occurred_at_~",
                table: "project_direction_reviews",
                columns: new[] { "project_direction_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_project_direction_reviews_reviewed_by_user_id",
                table: "project_direction_reviews",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_directions_reviewed_by_user_id",
                table: "project_directions",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_directions_team_id",
                table: "project_directions",
                column: "team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_proposal_history_performed_by_user_id",
                table: "team_proposal_history",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_proposal_history_proposal_id_occurred_at_utc",
                table: "team_proposal_history",
                columns: new[] { "proposal_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_team_proposal_members_class_id_student_id",
                table: "team_proposal_members",
                columns: new[] { "class_id", "student_id" },
                unique: true,
                filter: "counts_toward_open_proposal = true");

            migrationBuilder.CreateIndex(
                name: "IX_team_proposal_members_proposal_id",
                table: "team_proposal_members",
                column: "proposal_id",
                unique: true,
                filter: "is_leader = true");

            migrationBuilder.CreateIndex(
                name: "IX_team_proposals_approved_team_id",
                table: "team_proposals",
                column: "approved_team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_proposals_class_id_status",
                table: "team_proposals",
                columns: new[] { "class_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_team_proposals_proposed_by_student_id",
                table: "team_proposals",
                column: "proposed_by_student_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_proposals_reviewed_by_user_id",
                table: "team_proposals",
                column: "reviewed_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_direction_reviews");

            migrationBuilder.DropTable(
                name: "team_proposal_history");

            migrationBuilder.DropTable(
                name: "team_proposal_members");

            migrationBuilder.DropTable(
                name: "project_directions");

            migrationBuilder.DropTable(
                name: "team_proposals");

            migrationBuilder.DropIndex(
                name: "IX_team_members_class_id_student_id",
                table: "team_members");

            migrationBuilder.DropIndex(
                name: "IX_team_members_team_id",
                table: "team_members");

            migrationBuilder.DropIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments");

            migrationBuilder.DropColumn(
                name: "description",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "counts_toward_active_team",
                table: "team_members");

            migrationBuilder.AddColumn<Guid>(
                name: "leader_id",
                table: "teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "mentor_id",
                table: "teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE teams t
                SET leader_id = leader.student_id
                FROM (
                    SELECT DISTINCT ON (team_id) team_id, student_id
                    FROM team_members
                    WHERE role_in_team = 'Leader'
                    ORDER BY team_id, joined_at, student_id
                ) leader
                WHERE t.id = leader.team_id;
                """);

            migrationBuilder.Sql("""
                UPDATE teams t
                SET mentor_id = current_assignment.user_id
                FROM (
                    SELECT DISTINCT ON (ma.team_id) ma.team_id, mp.user_id
                    FROM mentor_assignments ma
                    INNER JOIN mentor_profiles mp ON mp.id = ma.mentor_profile_id
                    WHERE ma.status = 'Active' AND ma.is_deleted = FALSE
                    ORDER BY ma.team_id, ma.assigned_at DESC, ma.id
                ) current_assignment
                WHERE t.id = current_assignment.team_id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_teams_leader_id",
                table: "teams",
                column: "leader_id");

            migrationBuilder.CreateIndex(
                name: "IX_teams_mentor_id",
                table: "teams",
                column: "mentor_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_members_class_id_student_id",
                table: "team_members",
                columns: new[] { "class_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mentor_assignments_team_id",
                table: "mentor_assignments",
                column: "team_id");

            migrationBuilder.AddForeignKey(
                name: "FK_teams_students_leader_id",
                table: "teams",
                column: "leader_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_teams_users_mentor_id",
                table: "teams",
                column: "mentor_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

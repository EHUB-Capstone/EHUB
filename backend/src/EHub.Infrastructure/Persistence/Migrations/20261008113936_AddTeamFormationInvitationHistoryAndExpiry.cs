using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamFormationInvitationHistoryAndExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_team_formation_invitations",
                table: "team_formation_invitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_team_formation_invitations_status",
                table: "team_formation_invitations");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "team_formation_invitations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at_utc",
                table: "team_formation_invitations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at_utc",
                table: "team_formation_invitations",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill: unique ids before the new primary key, creation time from the owning formation,
            // and a fresh 24h window (from deploy time) for invitations that are still waiting.
            migrationBuilder.Sql("UPDATE team_formation_invitations SET id = gen_random_uuid();");
            migrationBuilder.Sql(@"
UPDATE team_formation_invitations AS invitation
SET created_at_utc = formation.created_at
FROM team_formations AS formation
WHERE formation.id = invitation.formation_id;");
            migrationBuilder.Sql(@"
UPDATE team_formation_invitations
SET expires_at_utc = now() + interval '24 hours'
WHERE status = 'Pending' AND reservation_released_at_utc IS NULL;");

            migrationBuilder.AddPrimaryKey(
                name: "PK_team_formation_invitations",
                table: "team_formation_invitations",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_team_formation_invitations_expires_at_utc",
                table: "team_formation_invitations",
                column: "expires_at_utc",
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_team_formation_invitations_formation_id_student_id",
                table: "team_formation_invitations",
                columns: new[] { "formation_id", "student_id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_team_formation_invitations_status",
                table: "team_formation_invitations",
                sql: "status IN ('Pending', 'Accepted', 'Declined', 'Expired', 'Left')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy: keep only the latest record per (formation, student) and map the new
            // statuses onto the old ones so the composite primary key can be restored.
            migrationBuilder.Sql(@"
DELETE FROM team_formation_invitations AS invitation
USING team_formation_invitations AS newer
WHERE newer.formation_id = invitation.formation_id
  AND newer.student_id = invitation.student_id
  AND (newer.created_at_utc, newer.id) > (invitation.created_at_utc, invitation.id);");
            migrationBuilder.Sql("UPDATE team_formation_invitations SET status = 'Declined' WHERE status IN ('Expired', 'Left');");

            migrationBuilder.DropPrimaryKey(
                name: "PK_team_formation_invitations",
                table: "team_formation_invitations");

            migrationBuilder.DropIndex(
                name: "IX_team_formation_invitations_expires_at_utc",
                table: "team_formation_invitations");

            migrationBuilder.DropIndex(
                name: "IX_team_formation_invitations_formation_id_student_id",
                table: "team_formation_invitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_team_formation_invitations_status",
                table: "team_formation_invitations");

            migrationBuilder.DropColumn(
                name: "id",
                table: "team_formation_invitations");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "team_formation_invitations");

            migrationBuilder.DropColumn(
                name: "expires_at_utc",
                table: "team_formation_invitations");

            migrationBuilder.AddPrimaryKey(
                name: "PK_team_formation_invitations",
                table: "team_formation_invitations",
                columns: new[] { "formation_id", "student_id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_team_formation_invitations_status",
                table: "team_formation_invitations",
                sql: "status IN ('Pending', 'Accepted', 'Declined')");
        }
    }
}

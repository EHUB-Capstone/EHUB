using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectProposalLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "project_proposals",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "project_proposal_versions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DraftSave");

            migrationBuilder.AddColumn<string>(
                name: "snapshot_schema_version",
                table: "project_proposal_versions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "project-proposal-snapshot-v1");

            migrationBuilder.CreateTable(
                name: "project_proposal_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    feedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_proposal_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_proposal_reviews_project_proposal_versions_proposal~",
                        column: x => x.proposal_version_id,
                        principalTable: "project_proposal_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposal_reviews_project_proposals_project_proposal~",
                        column: x => x.project_proposal_id,
                        principalTable: "project_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposal_reviews_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_versions_purpose_created_at",
                table: "project_proposal_versions",
                columns: new[] { "purpose", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_reviews_project_proposal_id_occurred_at_utc",
                table: "project_proposal_reviews",
                columns: new[] { "project_proposal_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_reviews_proposal_version_id",
                table: "project_proposal_reviews",
                column: "proposal_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_reviews_reviewed_by_user_id",
                table: "project_proposal_reviews",
                column: "reviewed_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_proposal_reviews");

            migrationBuilder.DropIndex(
                name: "IX_project_proposal_versions_purpose_created_at",
                table: "project_proposal_versions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "project_proposals");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "project_proposal_versions");

            migrationBuilder.DropColumn(
                name: "snapshot_schema_version",
                table: "project_proposal_versions");
        }
    }
}

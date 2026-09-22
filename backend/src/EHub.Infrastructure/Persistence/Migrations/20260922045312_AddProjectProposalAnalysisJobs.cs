using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectProposalAnalysisJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_proposal_analysis_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    candidate_scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    include_cross_semester = table.Column<bool>(type: "boolean", nullable: false),
                    language_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    configuration_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    available_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_proposal_analysis_jobs", x => x.id);
                    table.CheckConstraint("CK_ProjectProposalAnalysisJob_AttemptCount", "attempt_count >= 0");
                    table.ForeignKey(
                        name: "FK_project_proposal_analysis_jobs_project_proposal_versions_pr~",
                        column: x => x.proposal_version_id,
                        principalTable: "project_proposal_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposal_analysis_jobs_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_analysis_jobs_proposal_version_id",
                table: "project_proposal_analysis_jobs",
                column: "proposal_version_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_analysis_jobs_requested_by_user_id",
                table: "project_proposal_analysis_jobs",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_analysis_jobs_status_available_at_utc",
                table: "project_proposal_analysis_jobs",
                columns: new[] { "status", "available_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_analysis_jobs_status_lease_expires_at_utc",
                table: "project_proposal_analysis_jobs",
                columns: new[] { "status", "lease_expires_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_proposal_analysis_jobs");
        }
    }
}

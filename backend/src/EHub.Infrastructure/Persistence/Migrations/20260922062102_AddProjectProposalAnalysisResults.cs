using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectProposalAnalysisResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_proposal_analysis_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    overlap_risk = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    potential_differentiators_json = table.Column<string>(type: "jsonb", nullable: false),
                    limitations_json = table.Column<string>(type: "jsonb", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    output_schema_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_proposal_analysis_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_proposal_analysis_results_project_proposal_analysis~",
                        column: x => x.analysis_job_id,
                        principalTable: "project_proposal_analysis_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_analysis_results_analysis_job_id",
                table: "project_proposal_analysis_results",
                column: "analysis_job_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_proposal_analysis_results");
        }
    }
}

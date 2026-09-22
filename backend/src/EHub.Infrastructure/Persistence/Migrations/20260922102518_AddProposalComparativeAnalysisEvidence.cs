using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalComparativeAnalysisEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "differences_json",
                table: "project_proposal_analysis_matches",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "evidence_json",
                table: "project_proposal_analysis_matches",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "novel_elements_json",
                table: "project_proposal_analysis_matches",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "similarities_json",
                table: "project_proposal_analysis_matches",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "differences_json",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "evidence_json",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "novel_elements_json",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "similarities_json",
                table: "project_proposal_analysis_matches");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalHybridSimilarityScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "hybrid_scoring_version",
                table: "project_proposal_analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "legacy-unscored-v0");

            migrationBuilder.AddColumn<string>(
                name: "hybrid_weights_json",
                table: "project_proposal_analysis_results",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "lexical_scoring_version",
                table: "project_proposal_analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "legacy-unscored-v0");

            migrationBuilder.AddColumn<int>(
                name: "retrieval_candidate_count",
                table: "project_proposal_analysis_results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "hybrid_similarity",
                table: "project_proposal_analysis_matches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "jaccard_similarity",
                table: "project_proposal_analysis_matches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "tf_idf_similarity",
                table: "project_proposal_analysis_matches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectProposalAnalysisResult_RetrievalCandidateCount",
                table: "project_proposal_analysis_results",
                sql: "retrieval_candidate_count >= 0 AND retrieval_candidate_count <= 10");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectProposalAnalysisMatch_HybridScore",
                table: "project_proposal_analysis_matches",
                sql: "hybrid_similarity >= -1 AND hybrid_similarity <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectProposalAnalysisMatch_LexicalScores",
                table: "project_proposal_analysis_matches",
                sql: "tf_idf_similarity >= 0 AND tf_idf_similarity <= 1 AND jaccard_similarity >= 0 AND jaccard_similarity <= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectProposalAnalysisResult_RetrievalCandidateCount",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectProposalAnalysisMatch_HybridScore",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectProposalAnalysisMatch_LexicalScores",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "hybrid_scoring_version",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "hybrid_weights_json",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "lexical_scoring_version",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "retrieval_candidate_count",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "hybrid_similarity",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "jaccard_similarity",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "tf_idf_similarity",
                table: "project_proposal_analysis_matches");
        }
    }
}

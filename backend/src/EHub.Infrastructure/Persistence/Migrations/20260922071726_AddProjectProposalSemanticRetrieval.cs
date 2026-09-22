using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectProposalSemanticRetrieval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "embedding_dimension",
                table: "project_proposal_analysis_results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "project_proposal_analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "embedding_provider",
                table: "project_proposal_analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "retrieval_version",
                table: "project_proposal_analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "text_schema_version",
                table: "project_proposal_analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "project_proposal_analysis_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_proposal_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    semantic_similarity = table.Column<double>(type: "double precision", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_proposal_analysis_matches", x => x.id);
                    table.CheckConstraint("CK_ProjectProposalAnalysisMatch_Rank", "rank > 0");
                    table.CheckConstraint("CK_ProjectProposalAnalysisMatch_Similarity", "semantic_similarity >= -1 AND semantic_similarity <= 1");
                    table.ForeignKey(
                        name: "FK_project_proposal_analysis_matches_project_proposal_analysis~",
                        column: x => x.analysis_result_id,
                        principalTable: "project_proposal_analysis_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposal_analysis_matches_project_proposal_versions~",
                        column: x => x.candidate_proposal_version_id,
                        principalTable: "project_proposal_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_proposal_embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    text_schema_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dimension = table.Column<int>(type: "integer", nullable: false),
                    vector_json = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_proposal_embeddings", x => x.id);
                    table.CheckConstraint("CK_ProjectProposalEmbedding_Dimension", "dimension > 0 AND dimension <= 3072");
                    table.ForeignKey(
                        name: "FK_project_proposal_embeddings_project_proposal_versions_propo~",
                        column: x => x.proposal_version_id,
                        principalTable: "project_proposal_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_analysis_matches_analysis_result_id_candid~",
                table: "project_proposal_analysis_matches",
                columns: new[] { "analysis_result_id", "candidate_proposal_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_analysis_matches_analysis_result_id_rank",
                table: "project_proposal_analysis_matches",
                columns: new[] { "analysis_result_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_analysis_matches_candidate_proposal_versio~",
                table: "project_proposal_analysis_matches",
                column: "candidate_proposal_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_embeddings_content_hash_text_schema_versio~",
                table: "project_proposal_embeddings",
                columns: new[] { "content_hash", "text_schema_version", "provider", "model", "dimension" });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_embeddings_proposal_version_id",
                table: "project_proposal_embeddings",
                column: "proposal_version_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_proposal_analysis_matches");

            migrationBuilder.DropTable(
                name: "project_proposal_embeddings");

            migrationBuilder.DropColumn(
                name: "embedding_dimension",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "embedding_provider",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "retrieval_version",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "text_schema_version",
                table: "project_proposal_analysis_results");
        }
    }
}

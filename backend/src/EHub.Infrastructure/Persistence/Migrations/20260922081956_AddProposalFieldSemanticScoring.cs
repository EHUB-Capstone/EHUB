using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalFieldSemanticScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "field_scoring_version",
                table: "project_proposal_analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "field_text_schema_version",
                table: "project_proposal_analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "field_weights_json",
                table: "project_proposal_analysis_results",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<double>(
                name: "problem_similarity",
                table: "project_proposal_analysis_matches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "solution_similarity",
                table: "project_proposal_analysis_matches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "target_customer_similarity",
                table: "project_proposal_analysis_matches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "value_and_approach_similarity",
                table: "project_proposal_analysis_matches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "weighted_semantic_similarity",
                table: "project_proposal_analysis_matches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "project_proposal_field_embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_project_proposal_field_embeddings", x => x.id);
                    table.CheckConstraint("CK_ProjectProposalFieldEmbedding_Dimension", "dimension > 0 AND dimension <= 3072");
                    table.ForeignKey(
                        name: "FK_project_proposal_field_embeddings_project_proposal_versions~",
                        column: x => x.proposal_version_id,
                        principalTable: "project_proposal_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectProposalAnalysisMatch_FieldScores",
                table: "project_proposal_analysis_matches",
                sql: "problem_similarity >= -1 AND problem_similarity <= 1 AND solution_similarity >= -1 AND solution_similarity <= 1 AND target_customer_similarity >= -1 AND target_customer_similarity <= 1 AND value_and_approach_similarity >= -1 AND value_and_approach_similarity <= 1 AND weighted_semantic_similarity >= -1 AND weighted_semantic_similarity <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_field_embeddings_content_hash_text_schema_~",
                table: "project_proposal_field_embeddings",
                columns: new[] { "content_hash", "text_schema_version", "provider", "model", "dimension" });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_field_embeddings_proposal_version_id_field",
                table: "project_proposal_field_embeddings",
                columns: new[] { "proposal_version_id", "field" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_proposal_field_embeddings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectProposalAnalysisMatch_FieldScores",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "field_scoring_version",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "field_text_schema_version",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "field_weights_json",
                table: "project_proposal_analysis_results");

            migrationBuilder.DropColumn(
                name: "problem_similarity",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "solution_similarity",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "target_customer_similarity",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "value_and_approach_similarity",
                table: "project_proposal_analysis_matches");

            migrationBuilder.DropColumn(
                name: "weighted_semantic_similarity",
                table: "project_proposal_analysis_matches");
        }
    }
}

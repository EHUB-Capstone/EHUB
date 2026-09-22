using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ProjectProposalAnalysisMatchConfiguration : IEntityTypeConfiguration<ProjectProposalAnalysisMatch>
{
    public void Configure(EntityTypeBuilder<ProjectProposalAnalysisMatch> builder)
    {
        builder.ToTable("project_proposal_analysis_matches", table =>
        {
            table.HasCheckConstraint("CK_ProjectProposalAnalysisMatch_Rank", "rank > 0");
            table.HasCheckConstraint("CK_ProjectProposalAnalysisMatch_Similarity", "semantic_similarity >= -1 AND semantic_similarity <= 1");
            table.HasCheckConstraint("CK_ProjectProposalAnalysisMatch_FieldScores", "problem_similarity >= -1 AND problem_similarity <= 1 AND solution_similarity >= -1 AND solution_similarity <= 1 AND target_customer_similarity >= -1 AND target_customer_similarity <= 1 AND value_and_approach_similarity >= -1 AND value_and_approach_similarity <= 1 AND weighted_semantic_similarity >= -1 AND weighted_semantic_similarity <= 1");
            table.HasCheckConstraint("CK_ProjectProposalAnalysisMatch_LexicalScores", "tf_idf_similarity >= 0 AND tf_idf_similarity <= 1 AND jaccard_similarity >= 0 AND jaccard_similarity <= 1");
            table.HasCheckConstraint("CK_ProjectProposalAnalysisMatch_HybridScore", "hybrid_similarity >= -1 AND hybrid_similarity <= 1");
        });

        builder.HasKey(match => match.Id);
        builder.Property(match => match.Id).HasColumnName("id");
        builder.Property(match => match.AnalysisResultId).HasColumnName("analysis_result_id").IsRequired();
        builder.Property(match => match.CandidateProposalVersionId).HasColumnName("candidate_proposal_version_id").IsRequired();
        builder.Property(match => match.Rank).HasColumnName("rank").IsRequired();
        builder.Property(match => match.SemanticSimilarity).HasColumnName("semantic_similarity").IsRequired();
        builder.Property(match => match.ProblemSimilarity).HasColumnName("problem_similarity").IsRequired();
        builder.Property(match => match.SolutionSimilarity).HasColumnName("solution_similarity").IsRequired();
        builder.Property(match => match.TargetCustomerSimilarity).HasColumnName("target_customer_similarity").IsRequired();
        builder.Property(match => match.ValueAndApproachSimilarity).HasColumnName("value_and_approach_similarity").IsRequired();
        builder.Property(match => match.WeightedSemanticSimilarity).HasColumnName("weighted_semantic_similarity").IsRequired();
        builder.Property(match => match.TfIdfSimilarity).HasColumnName("tf_idf_similarity").IsRequired();
        builder.Property(match => match.JaccardSimilarity).HasColumnName("jaccard_similarity").IsRequired();
        builder.Property(match => match.HybridSimilarity).HasColumnName("hybrid_similarity").IsRequired();
        builder.Property(match => match.SimilaritiesJson).HasColumnName("similarities_json").HasColumnType("jsonb").IsRequired();
        builder.Property(match => match.DifferencesJson).HasColumnName("differences_json").HasColumnType("jsonb").IsRequired();
        builder.Property(match => match.NovelElementsJson).HasColumnName("novel_elements_json").HasColumnType("jsonb").IsRequired();
        builder.Property(match => match.EvidenceJson).HasColumnName("evidence_json").HasColumnType("jsonb").IsRequired();
        builder.Property(match => match.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(match => new { match.AnalysisResultId, match.Rank }).IsUnique();
        builder.HasIndex(match => new { match.AnalysisResultId, match.CandidateProposalVersionId }).IsUnique();
        builder.HasIndex(match => match.CandidateProposalVersionId);

        builder.HasOne(match => match.AnalysisResult)
            .WithMany(result => result.Matches)
            .HasForeignKey(match => match.AnalysisResultId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(match => match.CandidateProposalVersion)
            .WithMany()
            .HasForeignKey(match => match.CandidateProposalVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

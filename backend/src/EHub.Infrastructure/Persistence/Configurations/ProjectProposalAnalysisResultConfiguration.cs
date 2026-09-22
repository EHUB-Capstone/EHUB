using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ProjectProposalAnalysisResultConfiguration : IEntityTypeConfiguration<ProjectProposalAnalysisResult>
{
    public void Configure(EntityTypeBuilder<ProjectProposalAnalysisResult> builder)
    {
        builder.ToTable("project_proposal_analysis_results", table =>
            table.HasCheckConstraint("CK_ProjectProposalAnalysisResult_RetrievalCandidateCount", "retrieval_candidate_count >= 0 AND retrieval_candidate_count <= 10"));

        builder.HasKey(result => result.Id);
        builder.Property(result => result.Id).HasColumnName("id");
        builder.Property(result => result.AnalysisJobId).HasColumnName("analysis_job_id").IsRequired();
        builder.Property(result => result.Summary).HasColumnName("summary").HasMaxLength(2_000).IsRequired();
        builder.Property(result => result.OverlapRisk).HasColumnName("overlap_risk").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(result => result.PotentialDifferentiatorsJson).HasColumnName("potential_differentiators_json").HasColumnType("jsonb").IsRequired();
        builder.Property(result => result.LimitationsJson).HasColumnName("limitations_json").HasColumnType("jsonb").IsRequired();
        builder.Property(result => result.Provider).HasColumnName("provider").HasMaxLength(100).IsRequired();
        builder.Property(result => result.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
        builder.Property(result => result.PromptVersion).HasColumnName("prompt_version").HasMaxLength(100).IsRequired();
        builder.Property(result => result.OutputSchemaVersion).HasColumnName("output_schema_version").HasMaxLength(100).IsRequired();
        builder.Property(result => result.EmbeddingProvider).HasColumnName("embedding_provider").HasMaxLength(100).IsRequired();
        builder.Property(result => result.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(100).IsRequired();
        builder.Property(result => result.EmbeddingDimension).HasColumnName("embedding_dimension").IsRequired();
        builder.Property(result => result.TextSchemaVersion).HasColumnName("text_schema_version").HasMaxLength(100).IsRequired();
        builder.Property(result => result.RetrievalVersion).HasColumnName("retrieval_version").HasMaxLength(100).IsRequired();
        builder.Property(result => result.FieldScoringVersion).HasColumnName("field_scoring_version").HasMaxLength(100).IsRequired();
        builder.Property(result => result.FieldTextSchemaVersion).HasColumnName("field_text_schema_version").HasMaxLength(100).IsRequired();
        builder.Property(result => result.FieldWeightsJson).HasColumnName("field_weights_json").HasColumnType("jsonb").IsRequired();
        builder.Property(result => result.LexicalScoringVersion).HasColumnName("lexical_scoring_version").HasMaxLength(100).IsRequired();
        builder.Property(result => result.HybridScoringVersion).HasColumnName("hybrid_scoring_version").HasMaxLength(100).IsRequired();
        builder.Property(result => result.HybridWeightsJson).HasColumnName("hybrid_weights_json").HasColumnType("jsonb").IsRequired();
        builder.Property(result => result.RetrievalCandidateCount).HasColumnName("retrieval_candidate_count").IsRequired();
        builder.Property(result => result.GeneratedAtUtc).HasColumnName("generated_at_utc").IsRequired();

        builder.HasIndex(result => result.AnalysisJobId).IsUnique();

        builder.HasOne(result => result.AnalysisJob)
            .WithOne(job => job.Result)
            .HasForeignKey<ProjectProposalAnalysisResult>(result => result.AnalysisJobId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

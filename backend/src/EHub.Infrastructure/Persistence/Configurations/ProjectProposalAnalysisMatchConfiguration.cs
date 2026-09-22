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
        });

        builder.HasKey(match => match.Id);
        builder.Property(match => match.Id).HasColumnName("id");
        builder.Property(match => match.AnalysisResultId).HasColumnName("analysis_result_id").IsRequired();
        builder.Property(match => match.CandidateProposalVersionId).HasColumnName("candidate_proposal_version_id").IsRequired();
        builder.Property(match => match.Rank).HasColumnName("rank").IsRequired();
        builder.Property(match => match.SemanticSimilarity).HasColumnName("semantic_similarity").IsRequired();
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

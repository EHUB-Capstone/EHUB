using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ProjectProposalReviewConfiguration : IEntityTypeConfiguration<ProjectProposalReview>
{
    public void Configure(EntityTypeBuilder<ProjectProposalReview> builder)
    {
        builder.ToTable("project_proposal_reviews");
        builder.HasKey(review => review.Id);
        builder.Property(review => review.Id).HasColumnName("id");
        builder.Property(review => review.ProjectProposalId).HasColumnName("project_proposal_id").IsRequired();
        builder.Property(review => review.ProposalVersionId).HasColumnName("proposal_version_id").IsRequired();
        builder.Property(review => review.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(review => review.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(review => review.Feedback).HasColumnName("feedback").HasMaxLength(1_000).IsRequired();
        builder.Property(review => review.ReviewedByUserId).HasColumnName("reviewed_by_user_id").IsRequired();
        builder.Property(review => review.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();

        builder.HasIndex(review => new { review.ProjectProposalId, review.OccurredAtUtc });
        builder.HasIndex(review => review.ProposalVersionId);
        builder.HasIndex(review => review.ReviewedByUserId);

        builder.HasOne(review => review.ProjectProposal)
            .WithMany(proposal => proposal.Reviews)
            .HasForeignKey(review => review.ProjectProposalId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(review => review.ProposalVersion)
            .WithMany(version => version.Reviews)
            .HasForeignKey(review => review.ProposalVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(review => review.ReviewedByUser)
            .WithMany()
            .HasForeignKey(review => review.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

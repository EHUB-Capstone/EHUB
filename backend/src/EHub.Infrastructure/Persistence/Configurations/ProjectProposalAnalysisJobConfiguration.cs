using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ProjectProposalAnalysisJobConfiguration : IEntityTypeConfiguration<ProjectProposalAnalysisJob>
{
    public void Configure(EntityTypeBuilder<ProjectProposalAnalysisJob> builder)
    {
        builder.ToTable("project_proposal_analysis_jobs", table =>
            table.HasCheckConstraint("CK_ProjectProposalAnalysisJob_AttemptCount", "attempt_count >= 0"));

        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id");
        builder.Property(job => job.ProposalVersionId).HasColumnName("proposal_version_id").IsRequired();
        builder.Property(job => job.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(job => job.CandidateScope).HasColumnName("candidate_scope").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(job => job.IncludeCrossSemester).HasColumnName("include_cross_semester").IsRequired();
        builder.Property(job => job.LanguageMode).HasColumnName("language_mode").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(job => job.ConfigurationVersion).HasColumnName("configuration_version").HasMaxLength(100).IsRequired();
        builder.Property(job => job.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(job => job.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(job => job.AvailableAtUtc).HasColumnName("available_at_utc").IsRequired();
        builder.Property(job => job.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(job => job.ProcessingStartedAtUtc).HasColumnName("processing_started_at_utc");
        builder.Property(job => job.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        builder.Property(job => job.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
        builder.Property(job => job.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(job => job.FailedAtUtc).HasColumnName("failed_at_utc");
        builder.Property(job => job.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(100);

        builder.HasIndex(job => job.ProposalVersionId).IsUnique();
        builder.HasIndex(job => new { job.Status, job.AvailableAtUtc });
        builder.HasIndex(job => new { job.Status, job.LeaseExpiresAtUtc });
        builder.HasIndex(job => job.RequestedByUserId);

        builder.HasOne(job => job.ProposalVersion)
            .WithOne(version => version.AnalysisJob)
            .HasForeignKey<ProjectProposalAnalysisJob>(job => job.ProposalVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(job => job.RequestedByUser)
            .WithMany()
            .HasForeignKey(job => job.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public class SubmissionFileSummaryConfiguration : IEntityTypeConfiguration<SubmissionFileSummary>
{
    public void Configure(EntityTypeBuilder<SubmissionFileSummary> builder)
    {
        builder.ToTable("submission_file_summaries");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SubmissionFileId).HasColumnName("submission_file_id").IsRequired();
        builder.Property(item => item.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(item => item.CheckpointId).HasColumnName("checkpoint_id").IsRequired();
        builder.Property(item => item.FileVersionNumber).HasColumnName("file_version_number").IsRequired();

        // One summary per stored file version.
        builder.HasIndex(item => item.SubmissionFileId).IsUnique();
        builder.HasIndex(item => new { item.TeamId, item.CheckpointId });

        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(CheckpointSummaryStatus.Pending)
            .IsRequired();
        builder.Property(item => item.IsAutomatic).HasColumnName("is_automatic").HasDefaultValue(false).IsRequired();
        builder.Property(item => item.PreviewJson).HasColumnName("preview_json").HasColumnType("jsonb");
        builder.Property(item => item.ContentJson).HasColumnName("content_json").HasColumnType("jsonb");
        builder.Property(item => item.Model).HasColumnName("model").HasMaxLength(100);
        builder.Property(item => item.PromptVersion).HasColumnName("prompt_version").HasMaxLength(20);
        builder.Property(item => item.InputTruncated).HasColumnName("input_truncated").HasDefaultValue(false).IsRequired();
        builder.Property(item => item.RequestedById).HasColumnName("requested_by_id").IsRequired();
        builder.Property(item => item.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(item => item.GeneratedAtUtc).HasColumnName("generated_at_utc");
        builder.Property(item => item.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0).IsRequired();
        builder.Property(item => item.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(item => item.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(100);
        builder.Property(item => item.RowVersion).IsRowVersion().HasColumnName("xmin");

        // The background job polls for due Pending rows and expired Processing leases.
        builder.HasIndex(item => new { item.Status, item.NextAttemptAtUtc });

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(item => !item.IsDeleted);

        // A summary has no meaning without its file.
        builder.HasOne(item => item.SubmissionFile)
            .WithMany()
            .HasForeignKey(item => item.SubmissionFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

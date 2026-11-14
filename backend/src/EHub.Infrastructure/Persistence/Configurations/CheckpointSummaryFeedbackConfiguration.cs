using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public class CheckpointSummaryFeedbackConfiguration : IEntityTypeConfiguration<CheckpointSummaryFeedback>
{
    public void Configure(EntityTypeBuilder<CheckpointSummaryFeedback> builder)
    {
        builder.ToTable("checkpoint_summary_feedback");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SubmissionFileSummaryId).HasColumnName("submission_file_summary_id").IsRequired();
        builder.Property(item => item.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(item => item.Rating).HasColumnName("rating").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(30);
        builder.Property(item => item.PromptVersion).HasColumnName("prompt_version").HasMaxLength(20);
        builder.Property(item => item.Model).HasColumnName("model").HasMaxLength(100);
        builder.Property(item => item.SummaryGenerationId).HasColumnName("summary_generation_id");

        // One verdict per reviewer and summary. Clearing a verdict deletes the row for real, so this index never blocks a new one.
        builder.HasIndex(item => new { item.SubmissionFileSummaryId, item.UserId }).IsUnique();
        builder.HasIndex(item => new { item.PromptVersion, item.Rating });

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasOne(item => item.SubmissionFileSummary).WithMany().HasForeignKey(item => item.SubmissionFileSummaryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> builder)
    {
        builder.ToTable("evaluations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(e => e.SubmissionId)
            .HasColumnName("submission_id");

        builder.Property(e => e.RubricId)
            .HasColumnName("rubric_id")
            .IsRequired();

        builder.Property(e => e.EvaluatorId)
            .HasColumnName("evaluator_id")
            .IsRequired();

        builder.Property(e => e.EvaluatorRole)
            .HasColumnName("evaluator_role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.TotalScore)
            .HasColumnName("total_score")
            .HasColumnType("decimal(8,2)")
            .IsRequired();

        builder.Property(e => e.MaxTotalScore)
            .HasColumnName("max_total_score")
            .HasColumnType("decimal(8,2)")
            .IsRequired();

        builder.Property(e => e.OverallFeedback)
            .HasColumnName("overall_feedback")
            .HasMaxLength(2000);

        builder.Property(e => e.Strengths)
            .HasColumnName("strengths")
            .HasMaxLength(2000);

        builder.Property(e => e.Weaknesses)
            .HasColumnName("weaknesses")
            .HasMaxLength(2000);

        builder.Property(e => e.Suggestions)
            .HasColumnName("suggestions")
            .HasMaxLength(2000);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.SubmittedAt)
            .HasColumnName("submitted_at");

        builder.Property(e => e.PublishedAt)
            .HasColumnName("published_at");

        // Unique indexes
        builder.HasIndex(e => new { e.SubmissionId, e.RubricId, e.EvaluatorId })
            .IsUnique()
            .HasFilter("submission_id IS NOT NULL");

        // Audit & Soft Delete properties configuration
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Relationships configuration
        builder.HasOne(e => e.Project)
            .WithMany(p => p.Evaluations)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Submission)
            .WithMany(s => s.Evaluations)
            .HasForeignKey(e => e.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Rubric)
            .WithMany(r => r.Evaluations)
            .HasForeignKey(e => e.RubricId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Evaluator)
            .WithMany()
            .HasForeignKey(e => e.EvaluatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class SubmissionFeedbackConfiguration : IEntityTypeConfiguration<SubmissionFeedback>
{
    public void Configure(EntityTypeBuilder<SubmissionFeedback> builder)
    {
        builder.ToTable("submission_feedbacks");

        builder.HasKey(sf => sf.Id);
        builder.Property(sf => sf.Id).HasColumnName("id");

        builder.Property(sf => sf.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(sf => sf.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(sf => sf.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(sf => sf.ParentFeedbackId)
            .HasColumnName("parent_feedback_id");

        builder.Property(sf => sf.Resolved)
            .HasColumnName("resolved")
            .HasDefaultValue(false);

        builder.Property(sf => sf.ResolvedById)
            .HasColumnName("resolved_by_id");

        builder.Property(sf => sf.ResolvedAt)
            .HasColumnName("resolved_at");

        // Audit & Soft Delete properties configuration
        builder.Property(sf => sf.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(sf => sf.CreatedBy).HasColumnName("created_by");
        builder.Property(sf => sf.UpdatedAt).HasColumnName("updated_at");
        builder.Property(sf => sf.UpdatedBy).HasColumnName("updated_by");
        builder.Property(sf => sf.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(sf => sf.DeletedAt).HasColumnName("deleted_at");
        builder.Property(sf => sf.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(sf => !sf.IsDeleted);

        // Relationships configuration
        builder.HasOne(sf => sf.Submission)
            .WithMany(s => s.Feedbacks)
            .HasForeignKey(sf => sf.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sf => sf.Creator)
            .WithMany()
            .HasForeignKey(sf => sf.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sf => sf.ResolvedBy)
            .WithMany()
            .HasForeignKey(sf => sf.ResolvedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sf => sf.ParentFeedback)
            .WithMany()
            .HasForeignKey(sf => sf.ParentFeedbackId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class MentoringActionItemConfiguration : IEntityTypeConfiguration<MentoringActionItem>
{
    public void Configure(EntityTypeBuilder<MentoringActionItem> builder)
    {
        builder.ToTable("mentoring_action_items");

        builder.HasKey(ai => ai.Id);
        builder.Property(ai => ai.Id).HasColumnName("id");

        builder.Property(ai => ai.MentoringSessionId)
            .HasColumnName("mentoring_session_id")
            .IsRequired();

        builder.Property(ai => ai.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(ai => ai.AssigneeUserId)
            .HasColumnName("assignee_user_id");

        builder.Property(ai => ai.AssigneeStudentId)
            .HasColumnName("assignee_student_id");

        builder.Property(ai => ai.DueDate)
            .HasColumnName("due_date");

        builder.Property(ai => ai.Completed)
            .HasColumnName("completed")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(ai => ai.CompletedAt)
            .HasColumnName("completed_at");

        // Indexes
        builder.HasIndex(ai => ai.MentoringSessionId);
        builder.HasIndex(ai => ai.AssigneeUserId);
        builder.HasIndex(ai => ai.AssigneeStudentId);
        builder.HasIndex(ai => ai.DueDate);
        builder.HasIndex(ai => ai.Completed);

        // Check constraint: Cannot have both assignee_user_id and assignee_student_id
        builder.ToTable(t => t.HasCheckConstraint("CK_MentoringActionItem_SingleAssignee", 
            "NOT (assignee_user_id IS NOT NULL AND assignee_student_id IS NOT NULL)"));

        // Audit & Soft Delete properties configuration
        builder.Property(ai => ai.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(ai => ai.CreatedBy).HasColumnName("created_by");
        builder.Property(ai => ai.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ai => ai.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ai => ai.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(ai => ai.DeletedAt).HasColumnName("deleted_at");
        builder.Property(ai => ai.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(ai => !ai.IsDeleted);

        // Relationships configuration
        builder.HasOne(ai => ai.MentoringSession)
            .WithMany(s => s.ActionItems)
            .HasForeignKey(ai => ai.MentoringSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ai => ai.AssigneeUser)
            .WithMany()
            .HasForeignKey(ai => ai.AssigneeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ai => ai.AssigneeStudent)
            .WithMany(s => s.AssignedMentoringActionItems)
            .HasForeignKey(ai => ai.AssigneeStudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class WeeklyTaskConfiguration : IEntityTypeConfiguration<WeeklyTask>
{
    public void Configure(EntityTypeBuilder<WeeklyTask> builder)
    {
        builder.ToTable("weekly_tasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description");

        builder.Property(t => t.TaskType)
            .HasColumnName("task_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.WeekNumber)
            .HasColumnName("week_number")
            .IsRequired();

        builder.Property(t => t.CourseId)
            .HasColumnName("course_id");

        builder.Property(t => t.ClassId)
            .HasColumnName("class_id");

        builder.Property(t => t.TeamId)
            .HasColumnName("team_id");

        builder.Property(t => t.AssigneeStudentId)
            .HasColumnName("assignee_student_id");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.StartDate)
            .HasColumnName("start_date");

        builder.Property(t => t.DueDate)
            .HasColumnName("due_date");

        builder.Property(t => t.AttachmentsJson)
            .HasColumnName("attachments_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(t => t.ChecklistJson)
            .HasColumnName("checklist_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(t => t.Tags)
            .HasColumnName("tags")
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(t => t.IsTemplate)
            .HasColumnName("is_template")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.IsMandatory)
            .HasColumnName("is_mandatory")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.VisibleToStudents)
            .HasColumnName("visible_to_students")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.CompletionPercentage)
            .HasColumnName("completion_percentage")
            .IsRequired();

        builder.Property(t => t.EstimatedHours)
            .HasColumnName("estimated_hours")
            .HasPrecision(5, 2);

        builder.Property(t => t.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(t => t.UpdatedById)
            .HasColumnName("updated_by_id");

        // Indexes
        builder.HasIndex(t => t.CourseId);
        builder.HasIndex(t => t.ClassId);
        builder.HasIndex(t => t.TeamId);
        builder.HasIndex(t => t.AssigneeStudentId);
        builder.HasIndex(t => t.WeekNumber);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Priority);
        builder.HasIndex(t => t.IsTemplate);
        builder.HasIndex(t => t.Scope);
        builder.HasIndex(t => new { t.CourseId, t.WeekNumber });
        builder.HasIndex(t => new { t.ClassId, t.WeekNumber });
        builder.HasIndex(t => new { t.TeamId, t.WeekNumber });

        // Constraints
        builder.ToTable(table => table.HasCheckConstraint("CK_WeeklyTask_WeekNumberPositive", "week_number >= 1"));
        builder.ToTable(table => table.HasCheckConstraint("CK_WeeklyTask_CompletionPercentageRange", "completion_percentage >= 0 AND completion_percentage <= 100"));
        builder.ToTable(table => table.HasCheckConstraint("CK_WeeklyTask_EstimatedHoursNonNegative", "estimated_hours IS NULL OR estimated_hours >= 0"));

        // Audit & Soft Delete properties configuration
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);

        // Relationships configuration
        builder.HasOne(t => t.Course)
            .WithMany(c => c.WeeklyTasks)
            .HasForeignKey(t => t.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Class)
            .WithMany(c => c.WeeklyTasks)
            .HasForeignKey(t => t.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Team)
            .WithMany(team => team.WeeklyTasks)
            .HasForeignKey(t => t.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssigneeStudent)
            .WithMany(s => s.WeeklyTasks)
            .HasForeignKey(t => t.AssigneeStudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Creator)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Updater)
            .WithMany()
            .HasForeignKey(t => t.UpdatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

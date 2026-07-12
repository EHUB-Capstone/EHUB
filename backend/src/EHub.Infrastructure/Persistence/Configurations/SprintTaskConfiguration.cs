using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class SprintTaskConfiguration : IEntityTypeConfiguration<SprintTask>
{
    public void Configure(EntityTypeBuilder<SprintTask> builder)
    {
        builder.ToTable("sprint_tasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(t => t.ProjectId)
            .HasColumnName("project_id");

        builder.Property(t => t.MilestoneId)
            .HasColumnName("milestone_id");

        builder.Property(t => t.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description");

        builder.Property(t => t.AssigneeUserId)
            .HasColumnName("assignee_user_id");

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

        builder.Property(t => t.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(t => t.Position)
            .HasColumnName("position")
            .IsRequired();

        builder.Property(t => t.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(t => t.UpdatedById)
            .HasColumnName("updated_by_id");

        // Indexes
        builder.HasIndex(t => t.TeamId);
        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => t.MilestoneId);
        builder.HasIndex(t => t.AssigneeUserId);
        builder.HasIndex(t => t.AssigneeStudentId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Priority);
        builder.HasIndex(t => t.DueDate);
        builder.HasIndex(t => t.Position);
        builder.HasIndex(t => new { t.TeamId, t.Status });
        builder.HasIndex(t => new { t.MilestoneId, t.Position });

        // Constraints
        builder.ToTable(table => table.HasCheckConstraint("CK_SprintTask_PositionNonNegative", "position >= 0"));
        builder.ToTable(table => table.HasCheckConstraint("CK_SprintTask_SingleAssigneeType", 
            "NOT (assignee_user_id IS NOT NULL AND assignee_student_id IS NOT NULL)"));

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
        builder.HasOne(t => t.Team)
            .WithMany(team => team.SprintTasks)
            .HasForeignKey(t => t.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Project)
            .WithMany(p => p.SprintTasks)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Milestone)
            .WithMany(m => m.Tasks)
            .HasForeignKey(t => t.MilestoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssigneeUser)
            .WithMany()
            .HasForeignKey(t => t.AssigneeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssigneeStudent)
            .WithMany(s => s.SprintTasks)
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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("milestones");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(m => m.ProjectId)
            .HasColumnName("project_id");

        builder.Property(m => m.ClassId)
            .HasColumnName("class_id");

        builder.Property(m => m.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(m => m.Description)
            .HasColumnName("description");

        builder.Property(m => m.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(m => m.DueDate)
            .HasColumnName("due_date")
            .IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(m => m.Progress)
            .HasColumnName("progress")
            .IsRequired();

        builder.Property(m => m.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(m => m.UpdatedById)
            .HasColumnName("updated_by_id");

        // Indexes
        builder.HasIndex(m => m.TeamId);
        builder.HasIndex(m => m.ProjectId);
        builder.HasIndex(m => m.ClassId);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.DueDate);
        builder.HasIndex(m => m.CreatedById);
        builder.HasIndex(m => new { m.TeamId, m.Status });
        builder.HasIndex(m => new { m.ProjectId, m.Status });

        // Constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_Milestone_ProgressRange", "progress >= 0 AND progress <= 100"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Milestone_DueDateAfterOrEqualsStartDate", "due_date >= start_date"));

        // Audit & Soft Delete properties configuration
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(m => m.DeletedAt).HasColumnName("deleted_at");
        builder.Property(m => m.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(m => !m.IsDeleted);

        // Relationships configuration
        builder.HasOne(m => m.Team)
            .WithMany(t => t.Milestones)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Project)
            .WithMany(p => p.Milestones)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Class)
            .WithMany(c => c.Milestones)
            .HasForeignKey(m => m.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Creator)
            .WithMany()
            .HasForeignKey(m => m.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Updater)
            .WithMany()
            .HasForeignKey(m => m.UpdatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class WeeklyTaskTeamProgressConfiguration : IEntityTypeConfiguration<WeeklyTaskTeamProgress>
{
    public void Configure(EntityTypeBuilder<WeeklyTaskTeamProgress> builder)
    {
        builder.ToTable("weekly_task_team_progress");

        builder.HasKey(progress => progress.Id);

        builder.Property(progress => progress.Id)
            .HasColumnName("id");

        builder.Property(progress => progress.WeeklyTaskId)
            .HasColumnName("weekly_task_id")
            .IsRequired();

        builder.Property(progress => progress.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(progress => progress.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(progress => progress.ChecklistJson)
            .HasColumnName("checklist_json")
            .HasColumnType("jsonb");

        builder.Property(progress => progress.CompletionPercentage)
            .HasColumnName("completion_percentage")
            .IsRequired();

        builder.Property(progress => progress.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(progress => progress.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(progress => progress.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(progress => progress.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(progress => progress.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(progress => progress.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(progress => progress.DeletedBy)
            .HasColumnName("deleted_by");

        builder.HasIndex(progress => new { progress.WeeklyTaskId, progress.TeamId })
            .IsUnique();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_WeeklyTaskTeamProgress_CompletionPercentageRange",
            "completion_percentage >= 0 AND completion_percentage <= 100"));

        builder.HasQueryFilter(progress => !progress.IsDeleted);

        builder.HasOne(progress => progress.WeeklyTask)
            .WithMany(task => task.TeamProgress)
            .HasForeignKey(progress => progress.WeeklyTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(progress => progress.Team)
            .WithMany(team => team.WeeklyTaskProgress)
            .HasForeignKey(progress => progress.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

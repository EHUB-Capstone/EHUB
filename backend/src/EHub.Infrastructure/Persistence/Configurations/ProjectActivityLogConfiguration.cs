using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ProjectActivityLogConfiguration : IEntityTypeConfiguration<ProjectActivityLog>
{
    public void Configure(EntityTypeBuilder<ProjectActivityLog> builder)
    {
        builder.ToTable("project_activity_logs");
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Id).HasColumnName("id");
        builder.Property(activity => activity.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(activity => activity.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(activity => activity.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(activity => activity.Summary).HasColumnName("summary").HasMaxLength(300).IsRequired();
        builder.Property(activity => activity.ChangedFieldsJson).HasColumnName("changed_fields_json").HasColumnType("jsonb");
        builder.Property(activity => activity.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.HasIndex(activity => new { activity.ProjectId, activity.OccurredAtUtc })
            .HasDatabaseName("ix_project_activity_logs_project_occurred");
        builder.HasOne(activity => activity.Project)
            .WithMany(project => project.ActivityLogs)
            .HasForeignKey(activity => activity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(activity => activity.ActorUser)
            .WithMany()
            .HasForeignKey(activity => activity.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

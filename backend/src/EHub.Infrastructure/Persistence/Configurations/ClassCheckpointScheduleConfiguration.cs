using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ClassCheckpointScheduleConfiguration : IEntityTypeConfiguration<ClassCheckpointSchedule>
{
    public void Configure(EntityTypeBuilder<ClassCheckpointSchedule> builder)
    {
        builder.ToTable("class_checkpoint_schedules", table =>
            table.HasCheckConstraint("CK_class_checkpoint_schedules_date_range", "start_date_utc < end_date_utc"));

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.ClassId).HasColumnName("class_id").IsRequired();
        builder.Property(item => item.CheckpointId).HasColumnName("checkpoint_id").IsRequired();
        builder.Property(item => item.StartDateUtc).HasColumnName("start_date_utc").IsRequired();
        builder.Property(item => item.EndDateUtc).HasColumnName("end_date_utc").IsRequired();
        builder.Property(item => item.ReopenCount).HasColumnName("reopen_count").HasDefaultValue(0).IsRequired();
        builder.Property(item => item.LastReopenedAtUtc).HasColumnName("last_reopened_at_utc");

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(item => new { item.ClassId, item.CheckpointId }).IsUnique();
        builder.HasIndex(item => new { item.CheckpointId, item.StartDateUtc, item.EndDateUtc });
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasOne(item => item.Class)
            .WithMany(item => item.CheckpointSchedules)
            .HasForeignKey(item => item.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Checkpoint)
            .WithMany(item => item.ClassSchedules)
            .HasForeignKey(item => item.CheckpointId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

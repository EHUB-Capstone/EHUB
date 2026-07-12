using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.ToTable("classes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.ClassCode)
            .HasColumnName("class_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ClassIndex)
            .HasColumnName("class_index")
            .IsRequired();

        builder.Property(c => c.SemesterId)
            .HasColumnName("semester_id")
            .IsRequired();

        builder.Property(c => c.CourseId)
            .HasColumnName("course_id")
            .IsRequired();

        builder.Property(c => c.Room)
            .HasColumnName("room")
            .HasMaxLength(50);

        builder.Property(c => c.ScheduleJson)
            .HasColumnName("schedule_json")
            .HasColumnType("jsonb");

        builder.Property(c => c.IsMajorLocked)
            .HasColumnName("is_major_locked")
            .HasDefaultValue(false);

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CreatedById)
            .HasColumnName("created_by_id");

        // Composite unique indexes
        builder.HasIndex(c => new { c.ClassCode, c.SemesterId })
            .IsUnique();

        builder.HasIndex(c => new { c.SemesterId, c.CourseId, c.ClassIndex })
            .IsUnique();

        // Audit & Soft Delete properties configuration
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Relationships configuration
        builder.HasOne(c => c.Semester)
            .WithMany(s => s.Classes)
            .HasForeignKey(c => c.SemesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Course)
            .WithMany(co => co.Classes)
            .HasForeignKey(c => c.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Creator)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

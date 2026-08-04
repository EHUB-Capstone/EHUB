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

        builder.Property(c => c.PrimaryLecturerId)
            .HasColumnName("primary_lecturer_id");

        builder.Property(c => c.Room)
            .HasColumnName("room")
            .HasMaxLength(50);

        builder.Property(c => c.ScheduleJson)
            .HasColumnName("schedule_json")
            .HasColumnType("jsonb");

        builder.Property(c => c.IsEnrollmentMajorLocked)
            .HasColumnName("is_enrollment_major_locked")
            .HasDefaultValue(false);

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ArchivedAtUtc)
            .HasColumnName("archived_at_utc");

        builder.Property(c => c.ArchivedByUserId)
            .HasColumnName("archived_by_user_id");

        builder.Property(c => c.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(c => c.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        // Composite unique indexes & performance indexes
        builder.HasIndex(c => new { c.ClassCode, c.SemesterId })
            .IsUnique();

        builder.HasIndex(c => new { c.SemesterId, c.CourseId, c.ClassIndex })
            .IsUnique();

        builder.HasIndex(c => new { c.SemesterId, c.CourseId, c.PrimaryLecturerId, c.Status });

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

        builder.HasOne(c => c.PrimaryLecturer)
            .WithMany()
            .HasForeignKey(c => c.PrimaryLecturerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ArchivedByUser)
            .WithMany()
            .HasForeignKey(c => c.ArchivedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Creator)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

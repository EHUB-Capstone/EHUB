using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class MentoringAttendanceConfiguration : IEntityTypeConfiguration<MentoringAttendance>
{
    public void Configure(EntityTypeBuilder<MentoringAttendance> builder)
    {
        builder.ToTable("mentoring_attendances");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.MentoringSessionId)
            .HasColumnName("mentoring_session_id")
            .IsRequired();

        builder.Property(a => a.UserId)
            .HasColumnName("user_id");

        builder.Property(a => a.StudentId)
            .HasColumnName("student_id");

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(a => a.Email)
            .HasColumnName("email")
            .HasMaxLength(150);

        builder.Property(a => a.Attended)
            .HasColumnName("attended")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(a => a.CheckInAt)
            .HasColumnName("check_in_at");

        // Indexes
        builder.HasIndex(a => a.MentoringSessionId);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.StudentId);
        builder.HasIndex(a => a.Attended);

        // Unique indexes to prevent duplicate attendees
        builder.HasIndex(a => new { a.MentoringSessionId, a.UserId })
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");

        builder.HasIndex(a => new { a.MentoringSessionId, a.StudentId })
            .IsUnique()
            .HasFilter("student_id IS NOT NULL");

        // Check constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_MentoringAttendance_SingleParticipantType", 
            "NOT (user_id IS NOT NULL AND student_id IS NOT NULL)"));

        builder.ToTable(t => t.HasCheckConstraint("CK_MentoringAttendance_HasIdentification", 
            "user_id IS NOT NULL OR student_id IS NOT NULL OR name IS NOT NULL OR email IS NOT NULL"));

        // Audit & Soft Delete properties configuration
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(a => !a.IsDeleted);

        // Relationships configuration
        builder.HasOne(a => a.MentoringSession)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.MentoringSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Student)
            .WithMany(s => s.MentoringAttendances)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class WorkshopAttendanceConfiguration : IEntityTypeConfiguration<WorkshopAttendance>
{
    public void Configure(EntityTypeBuilder<WorkshopAttendance> builder)
    {
        builder.ToTable("workshop_attendances");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.WorkshopId)
            .HasColumnName("workshop_id")
            .IsRequired();

        builder.Property(a => a.UserId)
            .HasColumnName("user_id");

        builder.Property(a => a.StudentId)
            .HasColumnName("student_id");

        builder.Property(a => a.ClassId)
            .HasColumnName("class_id");

        builder.Property(a => a.Mode)
            .HasColumnName("mode")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.EvidenceUrl)
            .HasColumnName("evidence_url")
            .HasMaxLength(1000);

        builder.Property(a => a.CheckInAt)
            .HasColumnName("check_in_at");

        builder.Property(a => a.VerifiedById)
            .HasColumnName("verified_by_id");

        builder.Property(a => a.VerifiedAt)
            .HasColumnName("verified_at");

        builder.Property(a => a.RejectReason)
            .HasColumnName("reject_reason");

        // Indexes
        builder.HasIndex(a => a.WorkshopId);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.StudentId);
        builder.HasIndex(a => a.ClassId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.VerifiedById);
        builder.HasIndex(a => new { a.WorkshopId, a.UserId });
        builder.HasIndex(a => new { a.WorkshopId, a.StudentId });

        // Check constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_WorkshopAttendance_SingleParticipantType", 
            "NOT (user_id IS NOT NULL AND student_id IS NOT NULL)"));

        builder.ToTable(t => t.HasCheckConstraint("CK_WorkshopAttendance_HasIdentification", 
            "NOT (user_id IS NULL AND student_id IS NULL)"));

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
        builder.HasOne(a => a.Workshop)
            .WithMany(w => w.Attendances)
            .HasForeignKey(a => a.WorkshopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Student)
            .WithMany(s => s.WorkshopAttendances)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Class)
            .WithMany(c => c.WorkshopAttendances)
            .HasForeignKey(a => a.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.VerifiedBy)
            .WithMany()
            .HasForeignKey(a => a.VerifiedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

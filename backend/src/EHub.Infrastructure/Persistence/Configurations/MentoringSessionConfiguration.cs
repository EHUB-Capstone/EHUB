using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class MentoringSessionConfiguration : IEntityTypeConfiguration<MentoringSession>
{
    public void Configure(EntityTypeBuilder<MentoringSession> builder)
    {
        builder.ToTable("mentoring_sessions");

        builder.HasKey(ms => ms.Id);
        builder.Property(ms => ms.Id).HasColumnName("id");

        builder.Property(ms => ms.MentorAssignmentId)
            .HasColumnName("mentor_assignment_id")
            .IsRequired();

        builder.Property(ms => ms.LecturerUserId)
            .HasColumnName("lecturer_user_id");

        builder.Property(ms => ms.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ms => ms.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(ms => ms.StartAt)
            .HasColumnName("start_at")
            .IsRequired();

        builder.Property(ms => ms.EndAt)
            .HasColumnName("end_at")
            .IsRequired();

        builder.Property(ms => ms.Location)
            .HasColumnName("location")
            .HasMaxLength(300);

        builder.Property(ms => ms.MeetingUrl)
            .HasColumnName("meeting_url")
            .HasMaxLength(1000);

        builder.Property(ms => ms.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(ms => ms.Notes)
            .HasColumnName("notes");

        // Indexes
        builder.HasIndex(ms => ms.MentorAssignmentId);
        builder.HasIndex(ms => ms.LecturerUserId);
        builder.HasIndex(ms => ms.StartAt);
        builder.HasIndex(ms => ms.Status);
        builder.HasIndex(ms => new { ms.MentorAssignmentId, ms.StartAt });

        // Check constraint
        builder.ToTable(t => t.HasCheckConstraint("CK_MentoringSession_EndAtAfterStartAt", "end_at > start_at"));

        // Audit & Soft Delete properties configuration
        builder.Property(ms => ms.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(ms => ms.CreatedBy).HasColumnName("created_by");
        builder.Property(ms => ms.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ms => ms.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ms => ms.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(ms => ms.DeletedAt).HasColumnName("deleted_at");
        builder.Property(ms => ms.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(ms => !ms.IsDeleted);

        // Relationships configuration
        builder.HasOne(ms => ms.MentorAssignment)
            .WithMany(ma => ma.Sessions)
            .HasForeignKey(ms => ms.MentorAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ms => ms.Lecturer)
            .WithMany()
            .HasForeignKey(ms => ms.LecturerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

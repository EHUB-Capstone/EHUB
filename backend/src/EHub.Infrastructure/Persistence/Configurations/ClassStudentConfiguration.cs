using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ClassStudentConfiguration : IEntityTypeConfiguration<ClassStudent>
{
    public void Configure(EntityTypeBuilder<ClassStudent> builder)
    {
        builder.ToTable("class_students");

        builder.HasKey(cs => new { cs.ClassId, cs.StudentId });

        builder.Property(cs => cs.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(cs => cs.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(cs => cs.SemesterId)
            .HasColumnName("semester_id")
            .IsRequired();

        builder.Property(cs => cs.CourseId)
            .HasColumnName("course_id")
            .IsRequired();

        builder.Property(cs => cs.MemberCode)
            .HasColumnName("member_code")
            .HasMaxLength(50);

        builder.Property(cs => cs.EnrollmentStatus)
            .HasColumnName("enrollment_status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(cs => cs.CountsTowardCourseSemesterLimit)
            .HasColumnName("counts_toward_course_semester_limit")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(cs => cs.MajorCodeAtEnrollment)
            .HasColumnName("major_code_at_enrollment")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(cs => cs.MajorVerificationStatus)
            .HasColumnName("major_verification_status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(cs => cs.MajorVerifiedAtUtc)
            .HasColumnName("major_verified_at_utc");

        builder.Property(cs => cs.MajorVerifiedByUserId)
            .HasColumnName("major_verified_by_user_id");

        builder.Property(cs => cs.ExamDate)
            .HasColumnName("exam_date");

        builder.Property(cs => cs.ExamNote)
            .HasColumnName("exam_note")
            .HasMaxLength(500);

        builder.Property(cs => cs.Outcome1)
            .HasColumnName("outcome_1")
            .HasMaxLength(200);

        builder.Property(cs => cs.Outcome1Comment)
            .HasColumnName("outcome_1_comment")
            .HasMaxLength(1000);

        builder.Property(cs => cs.Outcome2)
            .HasColumnName("outcome_2")
            .HasMaxLength(200);

        builder.Property(cs => cs.Outcome2Comment)
            .HasColumnName("outcome_2_comment")
            .HasMaxLength(1000);

        builder.Property(cs => cs.Outcome3)
            .HasColumnName("outcome_3")
            .HasMaxLength(200);

        builder.Property(cs => cs.Outcome3Comment)
            .HasColumnName("outcome_3_comment")
            .HasMaxLength(1000);

        builder.Property(cs => cs.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(cs => cs.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Optional unique index: ClassId + MemberCode (enforces unique code in a class, if populated)
        // Wait, MemberCode can be null. In PostgreSQL, unique index on nullable columns allows multiple null values.
        // Let's create it as recommended, but we can make it a simple index if needed. The request says: "Có unique index phù hợp cho Class. Có composite key cho ClassStudent. Có unique index ClassId + MemberCode nếu MemberCode có dùng".
        // Let's configure it.
        builder.HasIndex(cs => new { cs.ClassId, cs.MemberCode })
            .IsUnique()
            .HasFilter("member_code IS NOT NULL"); // Filtered index to allow multiple null values in Postgres/EF Core!

        builder.HasIndex(cs => new { cs.StudentId, cs.SemesterId, cs.CourseId })
            .IsUnique()
            .HasFilter("counts_toward_course_semester_limit = true");

        // Relationships configuration
        builder.HasOne(cs => cs.Class)
            .WithMany(c => c.ClassStudents)
            .HasForeignKey(cs => cs.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cs => cs.Student)
            .WithMany(s => s.ClassStudents)
            .HasForeignKey(cs => cs.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cs => cs.MajorVerifiedByUser)
            .WithMany()
            .HasForeignKey(cs => cs.MajorVerifiedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

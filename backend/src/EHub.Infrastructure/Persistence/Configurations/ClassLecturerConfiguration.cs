using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ClassLecturerConfiguration : IEntityTypeConfiguration<ClassLecturer>
{
    public void Configure(EntityTypeBuilder<ClassLecturer> builder)
    {
        builder.ToTable("class_lecturers");

        builder.HasKey(cl => new { cl.ClassId, cl.LecturerId });

        builder.Property(cl => cl.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(cl => cl.LecturerId)
            .HasColumnName("lecturer_id")
            .IsRequired();

        builder.Property(cl => cl.IsPrimary)
            .HasColumnName("is_primary")
            .HasDefaultValue(true);

        builder.Property(cl => cl.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.Property(cl => cl.AssignedById)
            .HasColumnName("assigned_by_id");

        builder.HasIndex(cl => cl.ClassId)
            .IsUnique();

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "CK_class_lecturers_primary_only",
            "is_primary = true"));

        // Relationships configuration
        builder.HasOne(cl => cl.Class)
            .WithMany(c => c.ClassLecturers)
            .HasForeignKey(cl => cl.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cl => cl.Lecturer)
            .WithMany()
            .HasForeignKey(cl => cl.LecturerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cl => cl.AssignedBy)
            .WithMany()
            .HasForeignKey(cl => cl.AssignedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

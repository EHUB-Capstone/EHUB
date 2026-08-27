using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class SemesterStaffAssignmentConfiguration : IEntityTypeConfiguration<SemesterStaffAssignment>
{
    public void Configure(EntityTypeBuilder<SemesterStaffAssignment> builder)
    {
        builder.ToTable("semester_staff_assignments");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.SemesterId)
            .HasColumnName("semester_id")
            .IsRequired();
        builder.Property(item => item.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(item => item.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(item => item.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasIndex(item => new
            {
                item.SemesterId,
                item.UserId,
                item.Role
            })
            .IsUnique()
            .HasFilter("is_deleted = false");
        builder.HasIndex(item => new
            {
                item.SemesterId,
                item.Role,
                item.Status
            });

        builder.HasOne(item => item.Semester)
            .WithMany(semester => semester.StaffAssignments)
            .HasForeignKey(item => item.SemesterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.User)
            .WithMany(user => user.SemesterStaffAssignments)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(item => item.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(item => item.CreatedBy)
            .HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt)
            .HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy)
            .HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);
        builder.Property(item => item.DeletedAt)
            .HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy)
            .HasColumnName("deleted_by");

        builder.HasQueryFilter(item => !item.IsDeleted);
    }
}

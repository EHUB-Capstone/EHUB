using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.RollNumber)
            .HasColumnName("roll_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.NormalizedRollNumber)
            .HasColumnName("normalized_roll_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(s => s.Email)
            .HasColumnName("email")
            .HasMaxLength(150);

        builder.Property(s => s.Major)
            .HasColumnName("major")
            .HasMaxLength(100);

        builder.Property(s => s.ProgramGroup)
            .HasColumnName("program_group")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(500);

        builder.Property(s => s.UserId)
            .HasColumnName("user_id");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Unique indexes
        builder.HasIndex(s => s.RollNumber).IsUnique();
        builder.HasIndex(s => s.NormalizedRollNumber).IsUnique();
        builder.HasIndex(s => s.UserId).IsUnique();

        // Audit & Soft Delete properties configuration
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);

        // 1-to-1 relationship with User
        builder.HasOne(s => s.User)
            .WithOne()
            .HasForeignKey<Student>(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

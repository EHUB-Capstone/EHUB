using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class CheckpointConfiguration : IEntityTypeConfiguration<Checkpoint>
{
    public void Configure(EntityTypeBuilder<Checkpoint> builder)
    {
        builder.ToTable("checkpoints");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.CourseId)
            .HasColumnName("course_id");

        builder.Property(c => c.ClassId)
            .HasColumnName("class_id");

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.CheckpointNumber)
            .HasColumnName("checkpoint_number")
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(c => c.RequirementsJson).HasColumnName("requirements_json").HasDefaultValue("[]");

        builder.Property(c => c.OpenDate)
            .HasColumnName("open_date");

        builder.Property(c => c.DueDate)
            .HasColumnName("due_date");

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CreatedById)
            .HasColumnName("created_by_id");

        // Unique indexes
        builder.HasIndex(c => new { c.CourseId, c.CheckpointNumber })
            .IsUnique()
            .HasFilter("course_id IS NOT NULL AND class_id IS NULL");

        builder.HasIndex(c => new { c.ClassId, c.CheckpointNumber })
            .IsUnique()
            .HasFilter("class_id IS NOT NULL");

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
        builder.HasOne(c => c.Course)
            .WithMany()
            .HasForeignKey(c => c.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Class)
            .WithMany()
            .HasForeignKey(c => c.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Creator)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class RubricConfiguration : IEntityTypeConfiguration<Rubric>
{
    public void Configure(EntityTypeBuilder<Rubric> builder)
    {
        builder.ToTable("rubrics");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(r => r.CourseId)
            .HasColumnName("course_id");

        builder.Property(r => r.ClassId)
            .HasColumnName("class_id");

        builder.Property(r => r.CheckpointId)
            .HasColumnName("checkpoint_id");

        builder.Property(r => r.TotalWeight)
            .HasColumnName("total_weight")
            .HasColumnType("decimal(6,2)")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.CreatedById)
            .HasColumnName("created_by_id");

        // Unique indexes
        builder.HasIndex(r => new { r.CourseId, r.CheckpointId, r.Name })
            .IsUnique()
            .HasFilter("course_id IS NOT NULL AND class_id IS NULL");

        builder.HasIndex(r => new { r.ClassId, r.CheckpointId, r.Name })
            .IsUnique()
            .HasFilter("class_id IS NOT NULL");

        // Audit & Soft Delete properties configuration
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(r => !r.IsDeleted);

        // Relationships configuration
        builder.HasOne(r => r.Course)
            .WithMany(c => c.Rubrics)
            .HasForeignKey(r => r.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Class)
            .WithMany(c => c.Rubrics)
            .HasForeignKey(r => r.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Checkpoint)
            .WithMany(cp => cp.Rubrics)
            .HasForeignKey(r => r.CheckpointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Creator)
            .WithMany()
            .HasForeignKey(r => r.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

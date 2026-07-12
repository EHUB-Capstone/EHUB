using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class RubricCriterionConfiguration : IEntityTypeConfiguration<RubricCriterion>
{
    public void Configure(EntityTypeBuilder<RubricCriterion> builder)
    {
        builder.ToTable("rubric_criteria");

        builder.HasKey(rc => rc.Id);
        builder.Property(rc => rc.Id).HasColumnName("id");

        builder.Property(rc => rc.RubricId)
            .HasColumnName("rubric_id")
            .IsRequired();

        builder.Property(rc => rc.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(rc => rc.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(rc => rc.MaxScore)
            .HasColumnName("max_score")
            .HasColumnType("decimal(6,2)")
            .IsRequired();

        builder.Property(rc => rc.Weight)
            .HasColumnName("weight")
            .HasColumnType("decimal(6,2)")
            .IsRequired();

        builder.Property(rc => rc.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        // Unique indexes
        builder.HasIndex(rc => new { rc.RubricId, rc.DisplayOrder })
            .IsUnique();

        builder.HasIndex(rc => new { rc.RubricId, rc.Name })
            .IsUnique();

        // Audit & Soft Delete properties configuration
        builder.Property(rc => rc.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(rc => rc.CreatedBy).HasColumnName("created_by");
        builder.Property(rc => rc.UpdatedAt).HasColumnName("updated_at");
        builder.Property(rc => rc.UpdatedBy).HasColumnName("updated_by");
        builder.Property(rc => rc.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(rc => rc.DeletedAt).HasColumnName("deleted_at");
        builder.Property(rc => rc.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(rc => !rc.IsDeleted);

        // Relationships configuration
        builder.HasOne(rc => rc.Rubric)
            .WithMany(r => r.Criteria)
            .HasForeignKey(rc => rc.RubricId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

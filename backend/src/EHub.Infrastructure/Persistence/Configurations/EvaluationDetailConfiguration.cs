using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class EvaluationDetailConfiguration : IEntityTypeConfiguration<EvaluationDetail>
{
    public void Configure(EntityTypeBuilder<EvaluationDetail> builder)
    {
        builder.ToTable("evaluation_details");

        builder.HasKey(ed => ed.Id);
        builder.Property(ed => ed.Id).HasColumnName("id");

        builder.Property(ed => ed.EvaluationId)
            .HasColumnName("evaluation_id")
            .IsRequired();

        builder.Property(ed => ed.RubricCriterionId)
            .HasColumnName("rubric_criterion_id")
            .IsRequired();

        builder.Property(ed => ed.Score)
            .HasColumnName("score")
            .HasColumnType("decimal(6,2)")
            .IsRequired();

        builder.Property(ed => ed.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        // Unique indexes
        builder.HasIndex(ed => new { ed.EvaluationId, ed.RubricCriterionId })
            .IsUnique();

        // Audit & Soft Delete properties configuration
        builder.Property(ed => ed.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(ed => ed.CreatedBy).HasColumnName("created_by");
        builder.Property(ed => ed.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ed => ed.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ed => ed.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(ed => ed.DeletedAt).HasColumnName("deleted_at");
        builder.Property(ed => ed.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(ed => !ed.IsDeleted);

        // Relationships configuration
        builder.HasOne(ed => ed.Evaluation)
            .WithMany(e => e.Details)
            .HasForeignKey(ed => ed.EvaluationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ed => ed.RubricCriterion)
            .WithMany()
            .HasForeignKey(ed => ed.RubricCriterionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

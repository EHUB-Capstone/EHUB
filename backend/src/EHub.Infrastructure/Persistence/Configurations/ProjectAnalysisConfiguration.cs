using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ProjectAnalysisConfiguration : IEntityTypeConfiguration<ProjectAnalysis>
{
    public void Configure(EntityTypeBuilder<ProjectAnalysis> builder)
    {
        builder.ToTable("project_analyses");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(a => a.AnalysisType)
            .HasColumnName("analysis_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.StrengthsJson)
            .HasColumnName("strengths_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.WeaknessesJson)
            .HasColumnName("weaknesses_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.FeasibilityAnalysis)
            .HasColumnName("feasibility_analysis");

        builder.Property(a => a.MarketPotential)
            .HasColumnName("market_potential");

        builder.Property(a => a.RisksJson)
            .HasColumnName("risks_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.SimilarIdeasJson)
            .HasColumnName("similar_ideas_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.SuggestionsJson)
            .HasColumnName("suggestions_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.Score)
            .HasColumnName("score")
            .HasPrecision(5, 2);

        builder.Property(a => a.Model)
            .HasColumnName("model")
            .HasMaxLength(100);

        builder.Property(a => a.GeneratedById)
            .HasColumnName("generated_by_id");

        builder.Property(a => a.GeneratedAt)
            .HasColumnName("generated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(a => a.ProjectId);
        builder.HasIndex(a => a.AnalysisType);
        builder.HasIndex(a => a.GeneratedById);
        builder.HasIndex(a => a.GeneratedAt);
        builder.HasIndex(a => a.Score);
        builder.HasIndex(a => new { a.ProjectId, a.GeneratedAt });

        // Constraints
        builder.ToTable(table => table.HasCheckConstraint("CK_ProjectAnalysis_ScoreRange", "score IS NULL OR (score >= 0 AND score <= 100)"));

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
        builder.HasOne(a => a.Project)
            .WithMany(p => p.ProjectAnalyses)
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.GeneratedBy)
            .WithMany()
            .HasForeignKey(a => a.GeneratedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

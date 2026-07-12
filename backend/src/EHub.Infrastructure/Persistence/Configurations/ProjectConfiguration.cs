using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        // Enforce Team 1 - 1 Project (unique constraint on TeamId)
        builder.HasIndex(p => p.TeamId)
            .IsUnique();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(p => p.Problem)
            .HasColumnName("problem")
            .HasMaxLength(2000);

        builder.Property(p => p.Solution)
            .HasColumnName("solution")
            .HasMaxLength(2000);

        builder.Property(p => p.StartupField)
            .HasColumnName("startup_field")
            .HasMaxLength(100);

        builder.Property(p => p.BusinessModel)
            .HasColumnName("business_model")
            .HasMaxLength(2000);

        builder.Property(p => p.Technology)
            .HasColumnName("technology")
            .HasMaxLength(2000);

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.IsHighPotential)
            .HasColumnName("is_high_potential")
            .HasDefaultValue(false);

        builder.Property(p => p.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(p => p.SubmittedAt)
            .HasColumnName("submitted_at");

        // Audit & Soft Delete properties configuration
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(p => !p.IsDeleted);

        // Relationships configuration
        builder.HasOne(p => p.Team)
            .WithOne(t => t.Project)
            .HasForeignKey<Project>(p => p.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Creator)
            .WithMany()
            .HasForeignKey(p => p.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class StartupLineageConfiguration : IEntityTypeConfiguration<StartupLineage>
{
    public void Configure(EntityTypeBuilder<StartupLineage> builder)
    {
        builder.ToTable("startup_lineages");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.StartupName)
            .HasColumnName("startup_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.OriginalProjectId)
            .HasColumnName("original_project_id")
            .IsRequired();

        builder.Property(l => l.CurrentProjectId)
            .HasColumnName("current_project_id")
            .IsRequired();

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        // Indexes
        builder.HasIndex(l => l.StartupName);
        builder.HasIndex(l => l.OriginalProjectId);
        builder.HasIndex(l => l.CurrentProjectId);
        builder.HasIndex(l => l.Status);
        builder.HasIndex(l => l.CreatedById);

        // Audit & Soft Delete properties configuration
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(l => l.DeletedAt).HasColumnName("deleted_at");
        builder.Property(l => l.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(l => !l.IsDeleted);

        // Relationships configuration
        builder.HasOne(l => l.OriginalProject)
            .WithMany(p => p.OriginalLineages)
            .HasForeignKey(l => l.OriginalProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.CurrentProject)
            .WithMany(p => p.CurrentLineages)
            .HasForeignKey(l => l.CurrentProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Creator)
            .WithMany()
            .HasForeignKey(l => l.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

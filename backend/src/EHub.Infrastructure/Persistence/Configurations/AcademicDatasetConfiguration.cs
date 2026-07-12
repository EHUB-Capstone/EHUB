using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class AcademicDatasetConfiguration : IEntityTypeConfiguration<AcademicDataset>
{
    public void Configure(EntityTypeBuilder<AcademicDataset> builder)
    {
        builder.ToTable("academic_datasets");

        builder.HasKey(ad => ad.Id);
        builder.Property(ad => ad.Id).HasColumnName("id");

        builder.Property(ad => ad.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(ad => ad.StudentId)
            .HasColumnName("student_id");

        builder.Property(ad => ad.ProjectId)
            .HasColumnName("project_id");

        builder.Property(ad => ad.DatasetType)
            .HasColumnName("dataset_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ad => ad.DynamicFieldsJson)
            .HasColumnName("dynamic_fields_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(ad => ad.LastImportBatchId)
            .HasColumnName("last_import_batch_id");

        // Indexes
        builder.HasIndex(ad => ad.ClassId);
        builder.HasIndex(ad => ad.StudentId);
        builder.HasIndex(ad => ad.ProjectId);
        builder.HasIndex(ad => ad.DatasetType);
        builder.HasIndex(ad => ad.LastImportBatchId);
        builder.HasIndex(ad => new { ad.ClassId, ad.DatasetType });
        builder.HasIndex(ad => new { ad.ClassId, ad.StudentId, ad.DatasetType });
        builder.HasIndex(ad => new { ad.ClassId, ad.ProjectId, ad.DatasetType });

        // Audit & Soft Delete properties configuration
        builder.Property(ad => ad.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(ad => ad.CreatedBy).HasColumnName("created_by");
        builder.Property(ad => ad.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ad => ad.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ad => ad.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(ad => ad.DeletedAt).HasColumnName("deleted_at");
        builder.Property(ad => ad.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(ad => !ad.IsDeleted);

        // Relationships configuration
        builder.HasOne(ad => ad.Class)
            .WithMany(c => c.AcademicDatasets)
            .HasForeignKey(ad => ad.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ad => ad.Student)
            .WithMany(s => s.AcademicDatasets)
            .HasForeignKey(ad => ad.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ad => ad.Project)
            .WithMany(p => p.AcademicDatasets)
            .HasForeignKey(ad => ad.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ad => ad.LastImportBatch)
            .WithMany(b => b.AcademicDatasets)
            .HasForeignKey(ad => ad.LastImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

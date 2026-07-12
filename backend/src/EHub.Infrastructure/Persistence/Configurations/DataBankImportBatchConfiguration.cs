using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class DataBankImportBatchConfiguration : IEntityTypeConfiguration<DataBankImportBatch>
{
    public void Configure(EntityTypeBuilder<DataBankImportBatch> builder)
    {
        builder.ToTable("data_bank_import_batches");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");

        builder.Property(b => b.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(b => b.FileChecksum)
            .HasColumnName("file_checksum")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(b => b.UploadedById)
            .HasColumnName("uploaded_by_id")
            .IsRequired();

        builder.Property(b => b.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(b => b.SheetName)
            .HasColumnName("sheet_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.HeaderRow)
            .HasColumnName("header_row")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(b => b.RowsInserted)
            .HasColumnName("rows_inserted")
            .IsRequired();

        builder.Property(b => b.RowsUpdated)
            .HasColumnName("rows_updated")
            .IsRequired();

        builder.Property(b => b.RowsSkipped)
            .HasColumnName("rows_skipped")
            .IsRequired();

        builder.Property(b => b.ColumnsAdded)
            .HasColumnName("columns_added")
            .HasColumnType("text[]");

        builder.Property(b => b.ColumnsIgnored)
            .HasColumnName("columns_ignored")
            .HasColumnType("text[]");

        builder.Property(b => b.ConflictsJson)
            .HasColumnName("conflicts_json")
            .HasColumnType("jsonb");

        builder.Property(b => b.AnalysisJson)
            .HasColumnName("analysis_json")
            .HasColumnType("jsonb");

        builder.Property(b => b.ColumnMappingsJson)
            .HasColumnName("column_mappings_json")
            .HasColumnType("jsonb");

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.CommittedAt)
            .HasColumnName("committed_at");

        builder.Property(b => b.RolledBackAt)
            .HasColumnName("rolled_back_at");

        // Indexes
        builder.HasIndex(b => b.ClassId);
        builder.HasIndex(b => b.UploadedById);
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.CreatedAt);
        builder.HasIndex(b => b.FileChecksum);
        builder.HasIndex(b => new { b.ClassId, b.CreatedAt });
        builder.HasIndex(b => new { b.UploadedById, b.CreatedAt });
        builder.HasIndex(b => new { b.ClassId, b.FileChecksum });

        // Audit & Soft Delete properties configuration
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");
        builder.Property(b => b.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(b => b.DeletedAt).HasColumnName("deleted_at");
        builder.Property(b => b.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(b => !b.IsDeleted);

        // Relationships configuration
        builder.HasOne(b => b.Class)
            .WithMany(c => c.DataBankImportBatches)
            .HasForeignKey(b => b.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.UploadedBy)
            .WithMany()
            .HasForeignKey(b => b.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

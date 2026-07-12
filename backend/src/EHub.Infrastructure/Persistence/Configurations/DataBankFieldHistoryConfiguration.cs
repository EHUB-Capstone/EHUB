using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class DataBankFieldHistoryConfiguration : IEntityTypeConfiguration<DataBankFieldHistory>
{
    public void Configure(EntityTypeBuilder<DataBankFieldHistory> builder)
    {
        builder.ToTable("data_bank_field_histories");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id");

        builder.Property(h => h.DatasetId)
            .HasColumnName("dataset_id")
            .IsRequired();

        builder.Property(h => h.FieldKey)
            .HasColumnName("field_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(h => h.OldValueJson)
            .HasColumnName("old_value_json")
            .HasColumnType("jsonb");

        builder.Property(h => h.NewValueJson)
            .HasColumnName("new_value_json")
            .HasColumnType("jsonb");

        builder.Property(h => h.ImportBatchId)
            .HasColumnName("import_batch_id")
            .IsRequired();

        builder.Property(h => h.ImportedById)
            .HasColumnName("imported_by_id")
            .IsRequired();

        builder.Property(h => h.ImportedAt)
            .HasColumnName("imported_at")
            .IsRequired();

        builder.Property(h => h.EntityType)
            .HasColumnName("entity_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(h => h.DatasetId);
        builder.HasIndex(h => h.FieldKey);
        builder.HasIndex(h => h.ImportBatchId);
        builder.HasIndex(h => h.ImportedById);
        builder.HasIndex(h => h.ImportedAt);
        builder.HasIndex(h => new { h.DatasetId, h.FieldKey, h.ImportedAt });
        builder.HasIndex(h => new { h.ImportBatchId, h.ImportedAt });
        builder.HasIndex(h => new { h.EntityType, h.ImportedAt });

        // Relationships configuration
        builder.HasOne(h => h.Dataset)
            .WithMany(d => d.FieldHistories)
            .HasForeignKey(h => h.DatasetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.ImportBatch)
            .WithMany(b => b.FieldHistories)
            .HasForeignKey(h => h.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.ImportedBy)
            .WithMany()
            .HasForeignKey(h => h.ImportedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

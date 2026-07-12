using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class DataBankSnapshotConfiguration : IEntityTypeConfiguration<DataBankSnapshot>
{
    public void Configure(EntityTypeBuilder<DataBankSnapshot> builder)
    {
        builder.ToTable("data_bank_snapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.ImportBatchId)
            .HasColumnName("import_batch_id")
            .IsRequired();

        builder.Property(s => s.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(s => s.ScopeJson)
            .HasColumnName("scope_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.StudentSnapshotJson)
            .HasColumnName("student_snapshot_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.DatasetSnapshotJson)
            .HasColumnName("dataset_snapshot_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(s => s.ImportBatchId)
            .IsUnique();

        builder.HasIndex(s => s.CreatedById);
        builder.HasIndex(s => s.CreatedAt);

        // Relationships configuration
        builder.HasOne(s => s.ImportBatch)
            .WithOne(b => b.Snapshot)
            .HasForeignKey<DataBankSnapshot>(s => s.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

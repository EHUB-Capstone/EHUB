using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class DataBankColumnConfiguration : IEntityTypeConfiguration<DataBankColumn>
{
    public void Configure(EntityTypeBuilder<DataBankColumn> builder)
    {
        builder.ToTable("data_bank_columns");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.Key)
            .HasColumnName("column_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.NormalizedKey)
            .HasColumnName("normalized_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.DataType)
            .HasColumnName("data_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Aliases)
            .HasColumnName("aliases")
            .HasColumnType("text[]");

        builder.Property(c => c.NormalizedAliases)
            .HasColumnName("normalized_aliases")
            .HasColumnType("text[]");

        builder.Property(c => c.IsSystemField)
            .HasColumnName("is_system_field")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        // Indexes
        builder.HasIndex(c => c.NormalizedKey)
            .IsUnique();

        builder.HasIndex(c => c.IsActive);
        builder.HasIndex(c => c.DataType);

        // Audit & Soft Delete properties configuration
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

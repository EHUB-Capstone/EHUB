using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class DataBankExportTemplateConfiguration : IEntityTypeConfiguration<DataBankExportTemplate>
{
    public void Configure(EntityTypeBuilder<DataBankExportTemplate> builder)
    {
        builder.ToTable("data_bank_export_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(t => t.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(t => t.SelectedColumns)
            .HasColumnName("selected_columns")
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(t => t.ColumnOrder)
            .HasColumnName("column_order")
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(t => t.HeaderAliasesJson)
            .HasColumnName("header_aliases_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(t => t.FiltersJson)
            .HasColumnName("filters_json")
            .HasColumnType("jsonb")
            .IsRequired();

        // Indexes
        builder.HasIndex(t => t.OwnerId);
        builder.HasIndex(t => new { t.OwnerId, t.Name })
            .IsUnique();

        // Audit & Soft Delete properties configuration
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);

        // Relationships configuration
        builder.HasOne(t => t.Owner)
            .WithMany()
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

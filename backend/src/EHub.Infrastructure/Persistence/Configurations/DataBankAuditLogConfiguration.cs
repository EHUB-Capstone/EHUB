using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class DataBankAuditLogConfiguration : IEntityTypeConfiguration<DataBankAuditLog>
{
    public void Configure(EntityTypeBuilder<DataBankAuditLog> builder)
    {
        builder.ToTable("data_bank_audit_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(l => l.Action)
            .HasColumnName("action")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Entity)
            .HasColumnName("entity")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.EntityId)
            .HasColumnName("entity_id");

        builder.Property(l => l.DetailsJson)
            .HasColumnName("details_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(l => new { l.UserId, l.CreatedAt });
        builder.HasIndex(l => new { l.Action, l.CreatedAt });
        builder.HasIndex(l => new { l.Entity, l.EntityId });
        builder.HasIndex(l => l.CreatedAt);

        // Relationships configuration
        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

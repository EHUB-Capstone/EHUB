using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class EvaluationHistoryConfiguration : IEntityTypeConfiguration<EvaluationHistory>
{
    public void Configure(EntityTypeBuilder<EvaluationHistory> builder)
    {
        builder.ToTable("evaluation_histories");

        builder.HasKey(eh => eh.Id);
        builder.Property(eh => eh.Id).HasColumnName("id");

        builder.Property(eh => eh.EvaluationId)
            .HasColumnName("evaluation_id")
            .IsRequired();

        builder.Property(eh => eh.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(eh => eh.Action)
            .HasColumnName("action")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(eh => eh.SnapshotJson)
            .HasColumnName("snapshot_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(eh => eh.ChangedById)
            .HasColumnName("changed_by_id")
            .IsRequired();

        builder.Property(eh => eh.ChangedAt)
            .HasColumnName("changed_at")
            .IsRequired();

        builder.Property(eh => eh.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.Property(eh => eh.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Unique index
        builder.HasIndex(eh => new { eh.EvaluationId, eh.Version })
            .IsUnique();

        // Relationships configuration
        builder.HasOne(eh => eh.Evaluation)
            .WithMany(e => e.Histories)
            .HasForeignKey(eh => eh.EvaluationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(eh => eh.ChangedBy)
            .WithMany()
            .HasForeignKey(eh => eh.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

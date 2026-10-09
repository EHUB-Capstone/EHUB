using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnName("id");
        builder.Property(message => message.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(message => message.Type).HasColumnName("type").HasMaxLength(200).IsRequired();
        builder.Property(message => message.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
        builder.Property(message => message.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(message => message.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(message => message.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(message => message.AvailableAtUtc).HasColumnName("available_at_utc").IsRequired();
        builder.Property(message => message.ProcessingStartedAtUtc).HasColumnName("processing_started_at_utc");
        builder.Property(message => message.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(message => message.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(message => message.LastError).HasColumnName("last_error").HasMaxLength(2_000);

        builder.HasIndex(message => message.EventId).IsUnique();
        builder.HasIndex(message => new { message.Status, message.AvailableAtUtc });
        builder.HasIndex(message => new { message.AggregateType, message.AggregateId });
    }
}

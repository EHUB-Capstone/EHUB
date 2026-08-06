using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ClassAuditLogConfiguration : IEntityTypeConfiguration<ClassAuditLog>
{
    public void Configure(EntityTypeBuilder<ClassAuditLog> builder)
    {
        builder.ToTable("class_audit_logs");

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).HasColumnName("id");
        builder.Property(log => log.ClassId).HasColumnName("class_id").IsRequired();
        builder.Property(log => log.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(log => log.PerformedByUserId).HasColumnName("performed_by_user_id").IsRequired();
        builder.Property(log => log.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(log => log.DetailsJson).HasColumnName("details_json").HasColumnType("jsonb");

        builder.HasIndex(log => new { log.ClassId, log.OccurredAtUtc });

        builder.HasOne(log => log.Class)
            .WithMany(@class => @class.AuditLogs)
            .HasForeignKey(log => log.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(log => log.PerformedByUser)
            .WithMany()
            .HasForeignKey(log => log.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(log => !log.Class.IsDeleted);
    }
}

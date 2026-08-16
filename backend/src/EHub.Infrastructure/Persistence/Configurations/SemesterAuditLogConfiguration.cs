using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class SemesterAuditLogConfiguration : IEntityTypeConfiguration<SemesterAuditLog>
{
    public void Configure(EntityTypeBuilder<SemesterAuditLog> builder)
    {
        builder.ToTable("semester_audit_logs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).HasColumnName("id");
        builder.Property(log => log.SemesterId).HasColumnName("semester_id").IsRequired();
        builder.Property(log => log.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(log => log.PerformedByUserId).HasColumnName("performed_by_user_id").IsRequired();
        builder.Property(log => log.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(log => log.DetailsJson).HasColumnName("details_json").HasColumnType("jsonb");
        builder.HasIndex(log => new { log.SemesterId, log.OccurredAtUtc });
        builder.HasOne(log => log.Semester).WithMany(semester => semester.AuditLogs)
            .HasForeignKey(log => log.SemesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(log => log.PerformedByUser).WithMany()
            .HasForeignKey(log => log.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(log => !log.Semester.IsDeleted);
    }
}

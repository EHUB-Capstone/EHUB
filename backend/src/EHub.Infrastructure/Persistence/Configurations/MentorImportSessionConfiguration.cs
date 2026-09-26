using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class MentorImportSessionConfiguration : IEntityTypeConfiguration<MentorImportSession>
{
    public void Configure(EntityTypeBuilder<MentorImportSession> builder)
    {
        builder.ToTable("mentor_import_sessions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.AdminUserId).HasColumnName("admin_user_id").IsRequired();
        builder.Property(item => item.SemesterId).HasColumnName("semester_id").IsRequired();
        builder.Property(item => item.RowsJson).HasColumnName("rows_json").HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(item => item.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(item => item.ProcessingStartedAtUtc).HasColumnName("processing_started_at_utc");
        builder.Property(item => item.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(item => new { item.AdminUserId, item.Status });
        builder.HasIndex(item => item.ExpiresAtUtc);
        builder.HasOne(item => item.AdminUser).WithMany().HasForeignKey(item => item.AdminUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Semester).WithMany().HasForeignKey(item => item.SemesterId).OnDelete(DeleteBehavior.Cascade);
    }
}

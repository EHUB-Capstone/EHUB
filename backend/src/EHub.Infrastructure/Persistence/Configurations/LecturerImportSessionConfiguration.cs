using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class LecturerImportSessionConfiguration : IEntityTypeConfiguration<LecturerImportSession>
{
    public void Configure(EntityTypeBuilder<LecturerImportSession> builder)
    {
        builder.ToTable("lecturer_import_sessions");

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id");
        builder.Property(session => session.AdminUserId).HasColumnName("admin_user_id").IsRequired();
        builder.Property(session => session.RowsJson).HasColumnName("rows_json").HasColumnType("jsonb").IsRequired();
        builder.Property(session => session.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(session => session.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(session => session.ProcessingStartedAtUtc).HasColumnName("processing_started_at_utc");
        builder.Property(session => session.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.Property(session => session.Version).IsRowVersion().HasColumnName("xmin");

        builder.HasIndex(session => new { session.AdminUserId, session.Status });
        builder.HasIndex(session => session.ExpiresAtUtc);

        builder.HasOne(session => session.AdminUser)
            .WithMany()
            .HasForeignKey(session => session.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

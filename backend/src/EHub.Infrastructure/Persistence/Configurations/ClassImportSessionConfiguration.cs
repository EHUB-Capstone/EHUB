using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ClassImportSessionConfiguration : IEntityTypeConfiguration<ClassImportSession>
{
    public void Configure(EntityTypeBuilder<ClassImportSession> builder)
    {
        builder.ToTable("class_import_sessions");

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id");
        builder.Property(session => session.ClassId).HasColumnName("class_id").IsRequired();
        builder.Property(session => session.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(session => session.ValidRowsJson).HasColumnName("valid_rows_json").HasColumnType("jsonb").IsRequired();
        builder.Property(session => session.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(session => session.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(session => session.ProcessingStartedAtUtc).HasColumnName("processing_started_at_utc");
        builder.Property(session => session.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.Property(session => session.Version).IsRowVersion().HasColumnName("xmin");

        builder.HasIndex(session => new { session.ClassId, session.UserId, session.Status });
        builder.HasIndex(session => session.ExpiresAtUtc);

        // Match Class's soft-delete filter so a required Class relationship is
        // never silently removed from an otherwise visible import session.
        builder.HasQueryFilter(session => !session.Class.IsDeleted);

        builder.HasOne(session => session.Class)
            .WithMany(@class => @class.ImportSessions)
            .HasForeignKey(session => session.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(session => session.User)
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

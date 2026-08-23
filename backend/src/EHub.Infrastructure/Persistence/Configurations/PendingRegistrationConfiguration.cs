using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class PendingRegistrationConfiguration : IEntityTypeConfiguration<PendingRegistration>
{
    public void Configure(EntityTypeBuilder<PendingRegistration> builder)
    {
        builder.ToTable("pending_registrations");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
        builder.Property(item => item.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(item => item.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(item => item.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
        builder.Property(item => item.RoleName).HasColumnName("role_name").HasMaxLength(30).IsRequired();
        builder.Property(item => item.MajorCode).HasColumnName("major_code").HasMaxLength(50);
        builder.Property(item => item.OtpHash).HasColumnName("otp_hash").HasMaxLength(128).IsRequired();
        builder.Property(item => item.OtpExpiresAtUtc).HasColumnName("otp_expires_at_utc").IsRequired();
        builder.Property(item => item.FailedAttemptCount).HasColumnName("failed_attempt_count").IsRequired();
        builder.Property(item => item.ResendCount).HasColumnName("resend_count").IsRequired();
        builder.Property(item => item.LastSentAtUtc).HasColumnName("last_sent_at_utc");
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.CompletedUserId).HasColumnName("completed_user_id");
        builder.Property(item => item.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin");

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(item => item.NormalizedEmail).IsUnique();
        builder.HasIndex(item => new { item.Status, item.OtpExpiresAtUtc });
        builder.HasIndex(item => item.CompletedUserId);
        builder.HasQueryFilter(item => !item.IsDeleted);
    }
}

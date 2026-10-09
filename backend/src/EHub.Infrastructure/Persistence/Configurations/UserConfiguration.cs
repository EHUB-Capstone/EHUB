using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(u => u.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.HasIndex(u => u.NormalizedEmail)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(u => u.Phone)
            .HasColumnName("phone")
            .HasMaxLength(20);

        builder.Property(u => u.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(500);

        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.IsEmailVerified)
            .HasColumnName("is_email_verified")
            .HasDefaultValue(false);

        builder.Property(u => u.LastLoginAt)
            .HasColumnName("last_login_at");

        // Audit & Soft Delete properties configuration
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");
        builder.Property(u => u.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");
        builder.Property(u => u.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}

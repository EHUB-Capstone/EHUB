using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseAssistantUserKeyConfiguration : IEntityTypeConfiguration<ShowcaseAssistantUserKey>
{
    public void Configure(EntityTypeBuilder<ShowcaseAssistantUserKey> builder)
    {
        builder.ToTable("showcase_assistant_user_keys");

        // One key per user, so the primary key is also the guarantee that two requests cannot create two rows.
        builder.HasKey(item => item.UserId);
        builder.Property(item => item.UserId).HasColumnName("user_id");
        builder.Property(item => item.Ciphertext).HasColumnName("ciphertext").HasColumnType("bytea").IsRequired();
        builder.Property(item => item.Last4).HasColumnName("last4").HasMaxLength(4).IsRequired();
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin");

        // Removing a user removes their stored key with them.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

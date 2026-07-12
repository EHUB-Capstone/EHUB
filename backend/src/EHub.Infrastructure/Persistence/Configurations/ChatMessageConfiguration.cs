using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.ChatGroupId)
            .HasColumnName("chat_group_id")
            .IsRequired();

        builder.Property(m => m.SenderUserId)
            .HasColumnName("sender_user_id")
            .IsRequired();

        builder.Property(m => m.SenderName)
            .HasColumnName("sender_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.SenderRole)
            .HasColumnName("sender_role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Text)
            .HasColumnName("text");

        builder.Property(m => m.MessageType)
            .HasColumnName("message_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.AttachmentJson)
            .HasColumnName("attachment_json")
            .HasColumnType("jsonb");

        builder.Property(m => m.ReactionsJson)
            .HasColumnName("reactions_json")
            .HasColumnType("jsonb");

        builder.Property(m => m.MentionsJson)
            .HasColumnName("mentions_json")
            .HasColumnType("jsonb");

        builder.Property(m => m.IsEdited)
            .HasColumnName("is_edited")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(m => m.EditedAt)
            .HasColumnName("edited_at");

        builder.Property(m => m.IsRevoked)
            .HasColumnName("is_revoked")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(m => m.RevokedAt)
            .HasColumnName("revoked_at");

        // Indexes
        builder.HasIndex(m => m.ChatGroupId);
        builder.HasIndex(m => m.SenderUserId);
        builder.HasIndex(m => m.CreatedAt);
        builder.HasIndex(m => m.MessageType);
        builder.HasIndex(m => m.IsRevoked);
        builder.HasIndex(m => new { m.ChatGroupId, m.CreatedAt });
        builder.HasIndex(m => new { m.ChatGroupId, m.SenderUserId });

        // Audit & Soft Delete properties configuration
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(m => m.DeletedAt).HasColumnName("deleted_at");
        builder.Property(m => m.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(m => !m.IsDeleted);

        // Relationships configuration
        builder.HasOne(m => m.ChatGroup)
            .WithMany(g => g.Messages)
            .HasForeignKey(m => m.ChatGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.SenderUser)
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

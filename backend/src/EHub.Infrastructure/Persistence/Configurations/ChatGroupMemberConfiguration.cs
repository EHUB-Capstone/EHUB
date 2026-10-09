using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ChatGroupMemberConfiguration : IEntityTypeConfiguration<ChatGroupMember>
{
    public void Configure(EntityTypeBuilder<ChatGroupMember> builder)
    {
        builder.ToTable("chat_group_members");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.ChatGroupId)
            .HasColumnName("chat_group_id")
            .IsRequired();

        builder.Property(m => m.UserId)
            .HasColumnName("user_id");

        builder.Property(m => m.StudentId)
            .HasColumnName("student_id");

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Nickname)
            .HasColumnName("nickname")
            .HasMaxLength(100);

        builder.Property(m => m.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        builder.Property(m => m.LeftAt)
            .HasColumnName("left_at");

        builder.Property(m => m.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        // Indexes
        builder.HasIndex(m => m.ChatGroupId);
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.StudentId);
        builder.HasIndex(m => m.Role);
        builder.HasIndex(m => m.IsActive);
        builder.HasIndex(m => new { m.ChatGroupId, m.UserId })
            .IsUnique()
            .HasFilter("user_id IS NOT NULL AND is_deleted = false");
        builder.HasIndex(m => new { m.ChatGroupId, m.StudentId })
            .IsUnique()
            .HasFilter("student_id IS NOT NULL AND is_deleted = false");

        // Check constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_ChatGroupMember_SingleMemberType", 
            "NOT (user_id IS NOT NULL AND student_id IS NOT NULL)"));

        builder.ToTable(t => t.HasCheckConstraint("CK_ChatGroupMember_HasIdentification", 
            "NOT (user_id IS NULL AND student_id IS NULL)"));

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
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.ChatGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Student)
            .WithMany(s => s.ChatGroupMemberships)
            .HasForeignKey(m => m.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

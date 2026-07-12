using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ChatGroupConfiguration : IEntityTypeConfiguration<ChatGroup>
{
    public void Configure(EntityTypeBuilder<ChatGroup> builder)
    {
        builder.ToTable("chat_groups");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");

        builder.Property(g => g.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(g => g.TeamId)
            .HasColumnName("team_id");

        builder.Property(g => g.GroupName)
            .HasColumnName("group_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(g => g.GroupType)
            .HasColumnName("group_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(g => g.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        // Indexes
        builder.HasIndex(g => g.ClassId);
        builder.HasIndex(g => g.TeamId);
        builder.HasIndex(g => g.GroupType);
        builder.HasIndex(g => g.CreatedById);
        builder.HasIndex(g => new { g.ClassId, g.GroupType });
        builder.HasIndex(g => new { g.TeamId, g.GroupType });

        // Audit & Soft Delete properties configuration
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(g => g.CreatedBy).HasColumnName("created_by");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");
        builder.Property(g => g.UpdatedBy).HasColumnName("updated_by");
        builder.Property(g => g.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(g => g.DeletedAt).HasColumnName("deleted_at");
        builder.Property(g => g.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(g => !g.IsDeleted);

        // Relationships configuration
        builder.HasOne(g => g.Class)
            .WithMany(c => c.ChatGroups)
            .HasForeignKey(g => g.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Team)
            .WithMany(t => t.ChatGroups)
            .HasForeignKey(g => g.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Creator)
            .WithMany()
            .HasForeignKey(g => g.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

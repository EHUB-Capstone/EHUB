using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ProjectShortcutConfiguration : IEntityTypeConfiguration<ProjectShortcut>
{
    public void Configure(EntityTypeBuilder<ProjectShortcut> builder)
    {
        builder.ToTable("project_shortcuts");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(s => s.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Url)
            .HasColumnName("url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(s => s.ShortcutType)
            .HasColumnName("shortcut_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        // Indexes
        builder.HasIndex(s => s.ProjectId);
        builder.HasIndex(s => s.TeamId);
        builder.HasIndex(s => s.CreatedById);
        builder.HasIndex(s => s.ShortcutType);
        builder.HasIndex(s => new { s.ProjectId, s.Name });

        // Audit & Soft Delete properties configuration
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);

        // Relationships configuration
        builder.HasOne(s => s.Project)
            .WithMany(p => p.Shortcuts)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Team)
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Creator)
            .WithMany()
            .HasForeignKey(s => s.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

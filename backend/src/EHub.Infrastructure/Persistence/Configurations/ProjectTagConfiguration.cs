using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ProjectTagConfiguration : IEntityTypeConfiguration<ProjectTag>
{
    public void Configure(EntityTypeBuilder<ProjectTag> builder)
    {
        builder.ToTable("project_tags");

        builder.HasKey(pt => pt.Id);
        builder.Property(pt => pt.Id).HasColumnName("id");

        builder.Property(pt => pt.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(pt => pt.TagName)
            .HasColumnName("tag_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(pt => pt.NormalizedTagName)
            .HasColumnName("normalized_tag_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(pt => pt.TagType)
            .HasColumnName("tag_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(pt => pt.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(pt => pt.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Unique index: Prevent duplicate tags of the same type for a project
        builder.HasIndex(pt => new { pt.ProjectId, pt.TagType, pt.NormalizedTagName })
            .IsUnique();

        // Relationships configuration
        builder.HasOne(pt => pt.Project)
            .WithMany(p => p.ProjectTags)
            .HasForeignKey(pt => pt.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pt => pt.CreatedBy)
            .WithMany()
            .HasForeignKey(pt => pt.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

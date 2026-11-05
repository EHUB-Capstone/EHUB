using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ShowcaseProjectConfiguration : IEntityTypeConfiguration<ShowcaseProject>
{
    public void Configure(EntityTypeBuilder<ShowcaseProject> builder)
    {
        builder.ToTable("showcase_projects");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.ProjectLineageId).HasColumnName("project_lineage_id").IsRequired();
        builder.Property(s => s.LogoUrl).HasColumnName("logo_url").HasMaxLength(1000);
        builder.Property(s => s.Highlights).HasColumnName("highlights").HasMaxLength(2000);

        builder.Property(s => s.DevelopmentStage)
            .HasColumnName("development_stage")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.ShowTeamMembers).HasColumnName("show_team_members").HasDefaultValue(true);
        builder.Property(s => s.PublishedAt).HasColumnName("published_at");
        builder.Property(s => s.ReviewNote).HasColumnName("review_note").HasMaxLength(1000);

        builder.Property(s => s.ViewCount).HasColumnName("view_count").HasDefaultValue(0);
        builder.Property(s => s.LikeCount).HasColumnName("like_count").HasDefaultValue(0);
        builder.Property(s => s.CommentCount).HasColumnName("comment_count").HasDefaultValue(0);

        builder.Property(s => s.Version).IsRowVersion().HasColumnName("xmin");

        // One live showcase per project lineage; soft-deleted rows are excluded so a lineage can be re-created.
        builder.HasIndex(s => s.ProjectLineageId)
            .IsUnique()
            .HasFilter("is_deleted = false");
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.PublishedAt);

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

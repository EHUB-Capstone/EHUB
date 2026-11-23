using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseModerationScanConfiguration : IEntityTypeConfiguration<ShowcaseModerationScan>
{
    public void Configure(EntityTypeBuilder<ShowcaseModerationScan> builder)
    {
        builder.ToTable("showcase_moderation_scans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ShowcaseProjectId).HasColumnName("showcase_project_id");
        builder.Property(x => x.CommentId).HasColumnName("comment_id");
        builder.Property(x => x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ScannedAt).HasColumnName("scanned_at");
        builder.Property(x => x.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(x => x.ShowcaseProjectId).IsUnique().HasFilter("comment_id IS NULL");
        builder.HasIndex(x => x.CommentId).IsUnique().HasFilter("comment_id IS NOT NULL");
        builder.HasOne<ShowcaseProject>().WithMany().HasForeignKey(x => x.ShowcaseProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ShowcaseComment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
    }
}

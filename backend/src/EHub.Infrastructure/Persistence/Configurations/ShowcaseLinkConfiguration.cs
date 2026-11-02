using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ShowcaseLinkConfiguration : IEntityTypeConfiguration<ShowcaseLink>
{
    public void Configure(EntityTypeBuilder<ShowcaseLink> builder)
    {
        builder.ToTable("showcase_links");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.ShowcaseProjectId).HasColumnName("showcase_project_id").IsRequired();
        builder.Property(l => l.LinkType)
            .HasColumnName("link_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(l => l.Label).HasColumnName("label").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Url).HasColumnName("url").HasMaxLength(1000).IsRequired();
        builder.Property(l => l.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(l => l.ShowcaseProjectId);

        builder.HasOne(l => l.ShowcaseProject)
            .WithMany(s => s.Links)
            .HasForeignKey(l => l.ShowcaseProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

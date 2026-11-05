using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ShowcaseMediaConfiguration : IEntityTypeConfiguration<ShowcaseMedia>
{
    public void Configure(EntityTypeBuilder<ShowcaseMedia> builder)
    {
        builder.ToTable("showcase_media");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.ShowcaseProjectId).HasColumnName("showcase_project_id").IsRequired();
        builder.Property(m => m.MediaType)
            .HasColumnName("media_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(m => m.Url).HasColumnName("url").HasMaxLength(1000).IsRequired();
        builder.Property(m => m.Caption).HasColumnName("caption").HasMaxLength(300);
        builder.Property(m => m.SortOrder).HasColumnName("sort_order");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(m => m.ShowcaseProjectId);

        builder.HasOne(m => m.ShowcaseProject)
            .WithMany(s => s.Media)
            .HasForeignKey(m => m.ShowcaseProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

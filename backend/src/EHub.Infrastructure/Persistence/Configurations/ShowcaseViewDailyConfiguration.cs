using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseViewDailyConfiguration : IEntityTypeConfiguration<ShowcaseViewDaily>
{
    public void Configure(EntityTypeBuilder<ShowcaseViewDaily> builder)
    {
        builder.ToTable("showcase_view_daily");
        builder.HasKey(view => new { view.ShowcaseProjectId, view.UserId, view.ViewDateUtc });
        builder.Property(view => view.ShowcaseProjectId).HasColumnName("showcase_project_id");
        builder.Property(view => view.UserId).HasColumnName("user_id");
        builder.Property(view => view.ViewDateUtc).HasColumnName("view_date_utc").HasColumnType("date");
        builder.HasIndex(view => view.UserId);

        builder.HasOne<ShowcaseProject>()
            .WithMany()
            .HasForeignKey(view => view.ShowcaseProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(view => view.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

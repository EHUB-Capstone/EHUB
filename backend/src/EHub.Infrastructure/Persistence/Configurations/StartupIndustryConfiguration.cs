using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class StartupIndustryConfiguration : IEntityTypeConfiguration<StartupIndustry>
{
    public void Configure(EntityTypeBuilder<StartupIndustry> builder)
    {
        builder.ToTable("startup_industries");
        builder.HasKey(industry => industry.Id);

        builder.Property(industry => industry.Id).HasColumnName("id");
        builder.Property(industry => industry.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(industry => industry.NormalizedName).HasColumnName("normalized_name").HasMaxLength(100).IsRequired();
        builder.Property(industry => industry.Description).HasColumnName("description").HasMaxLength(240);
        builder.Property(industry => industry.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(industry => industry.NormalizedName).IsUnique();

        builder.Property(industry => industry.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(industry => industry.CreatedBy).HasColumnName("created_by");
        builder.Property(industry => industry.UpdatedAt).HasColumnName("updated_at");
        builder.Property(industry => industry.UpdatedBy).HasColumnName("updated_by");
        builder.Property(industry => industry.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(industry => industry.DeletedAt).HasColumnName("deleted_at");
        builder.Property(industry => industry.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(industry => !industry.IsDeleted);
    }
}

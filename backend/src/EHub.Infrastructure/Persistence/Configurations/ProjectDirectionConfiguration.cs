using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ProjectDirectionConfiguration : IEntityTypeConfiguration<ProjectDirection>
{
    public void Configure(EntityTypeBuilder<ProjectDirection> builder)
    {
        builder.ToTable("project_directions");
        builder.HasKey(direction => direction.Id);
        builder.Property(direction => direction.Id).HasColumnName("id");
        builder.Property(direction => direction.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(direction => direction.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(direction => direction.Summary).HasColumnName("summary").HasMaxLength(5_000).IsRequired();
        builder.Property(direction => direction.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(direction => direction.SubmittedAtUtc).HasColumnName("submitted_at_utc");
        builder.Property(direction => direction.ReviewedAtUtc).HasColumnName("reviewed_at_utc");
        builder.Property(direction => direction.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(direction => direction.Version).IsRowVersion().HasColumnName("xmin");
        builder.Property(direction => direction.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(direction => direction.CreatedBy).HasColumnName("created_by");
        builder.Property(direction => direction.UpdatedAt).HasColumnName("updated_at");
        builder.Property(direction => direction.UpdatedBy).HasColumnName("updated_by");
        builder.Property(direction => direction.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(direction => direction.DeletedAt).HasColumnName("deleted_at");
        builder.Property(direction => direction.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(direction => !direction.IsDeleted);
        builder.HasIndex(direction => direction.TeamId).IsUnique();
        builder.HasOne(direction => direction.Team).WithOne(team => team.ProjectDirection).HasForeignKey<ProjectDirection>(direction => direction.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(direction => direction.ReviewedByUser).WithMany().HasForeignKey(direction => direction.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

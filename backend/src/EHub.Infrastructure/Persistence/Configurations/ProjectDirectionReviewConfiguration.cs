using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ProjectDirectionReviewConfiguration : IEntityTypeConfiguration<ProjectDirectionReview>
{
    public void Configure(EntityTypeBuilder<ProjectDirectionReview> builder)
    {
        builder.ToTable("project_direction_reviews");
        builder.HasKey(review => review.Id);
        builder.Property(review => review.Id).HasColumnName("id");
        builder.Property(review => review.ProjectDirectionId).HasColumnName("project_direction_id");
        builder.Property(review => review.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(30);
        builder.Property(review => review.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(30);
        builder.Property(review => review.Comment).HasColumnName("comment").HasMaxLength(1_000).IsRequired();
        builder.Property(review => review.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(review => review.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.HasIndex(review => new { review.ProjectDirectionId, review.OccurredAtUtc });
        builder.HasOne(review => review.ProjectDirection).WithMany(direction => direction.Reviews).HasForeignKey(review => review.ProjectDirectionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(review => review.ReviewedByUser).WithMany().HasForeignKey(review => review.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

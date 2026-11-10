using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public class CheckpointReviewMarkerConfiguration : IEntityTypeConfiguration<CheckpointReviewMarker>
{
    public void Configure(EntityTypeBuilder<CheckpointReviewMarker> builder)
    {
        builder.ToTable("checkpoint_review_markers");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(item => item.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(item => item.CheckpointId).HasColumnName("checkpoint_id").IsRequired();
        builder.Property(item => item.SeenVersionNumber).HasColumnName("seen_version_number").IsRequired();
        builder.Property(item => item.SeenAtUtc).HasColumnName("seen_at_utc").IsRequired();

        // One marker per person, team and checkpoint; the review desk reads all of a person's markers at once.
        builder.HasIndex(item => new { item.UserId, item.TeamId, item.CheckpointId }).IsUnique();

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasOne(item => item.Team).WithMany().HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Checkpoint).WithMany().HasForeignKey(item => item.CheckpointId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class CheckpointDeadlineExtensionRequestConfiguration : IEntityTypeConfiguration<CheckpointDeadlineExtensionRequest>
{
    public void Configure(EntityTypeBuilder<CheckpointDeadlineExtensionRequest> builder)
    {
        builder.ToTable("checkpoint_deadline_extension_requests");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(item => item.ClassId).HasColumnName("class_id").IsRequired();
        builder.Property(item => item.CheckpointId).HasColumnName("checkpoint_id").IsRequired();
        builder.Property(item => item.RequestedById).HasColumnName("requested_by_id").IsRequired();
        builder.Property(item => item.DeadlineUtc).HasColumnName("deadline_utc").IsRequired();
        builder.Property(item => item.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(2000).IsRequired();
        builder.HasIndex(item => new { item.TeamId, item.CheckpointId, item.DeadlineUtc }).IsUnique();
        builder.HasIndex(item => new { item.ClassId, item.RequestedAtUtc });
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasOne(item => item.Team).WithMany().HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Class).WithMany().HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Checkpoint).WithMany().HasForeignKey(item => item.CheckpointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RequestedBy).WithMany().HasForeignKey(item => item.RequestedById).OnDelete(DeleteBehavior.Restrict);
    }
}

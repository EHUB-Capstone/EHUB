using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class MentorAllocationJobConfiguration : IEntityTypeConfiguration<MentorAllocationJob>
{
    public void Configure(EntityTypeBuilder<MentorAllocationJob> builder)
    {
        builder.ToTable("mentor_allocation_jobs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.AdminUserId).HasColumnName("admin_user_id");
        builder.Property(item => item.SemesterId).HasColumnName("semester_id");
        builder.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(item => item.RequestJson).HasColumnName("request_json").HasColumnType("jsonb");
        builder.Property(item => item.ResultJson).HasColumnName("result_json").HasColumnType("jsonb");
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.CompletedBatches).HasColumnName("completed_batches");
        builder.Property(item => item.TotalBatches).HasColumnName("total_batches");
        builder.Property(item => item.Attempts).HasColumnName("attempts");
        builder.Property(item => item.ErrorMessage).HasColumnName("error_message").HasMaxLength(500);
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(item => item.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(item => item.LeaseId).HasColumnName("lease_id");
        builder.Property(item => item.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(item => new { item.AdminUserId, item.RequestHash }).IsUnique()
            .HasFilter("status IN ('Queued', 'Processing')");
        builder.HasIndex(item => new { item.Status, item.CreatedAtUtc });
        builder.HasIndex(item => item.ExpiresAtUtc);
        builder.HasOne(item => item.AdminUser).WithMany().HasForeignKey(item => item.AdminUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Semester).WithMany().HasForeignKey(item => item.SemesterId).OnDelete(DeleteBehavior.Cascade);
    }
}

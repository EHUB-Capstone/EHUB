using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public class SubmissionUploadSessionConfiguration : IEntityTypeConfiguration<SubmissionUploadSession>
{
    public void Configure(EntityTypeBuilder<SubmissionUploadSession> builder)
    {
        builder.ToTable("submission_upload_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(s => s.CheckpointId).HasColumnName("checkpoint_id").IsRequired();
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(s => s.ObjectKey).HasColumnName("object_key").HasMaxLength(512).IsRequired();
        builder.Property(s => s.OriginalName).HasColumnName("original_name").HasMaxLength(256).IsRequired();
        builder.Property(s => s.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(s => s.DeclaredSize).HasColumnName("declared_size").IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(s => s.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(s => s.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(s => s.SubmissionFileId).HasColumnName("submission_file_id");

        builder.Property(s => s.RowVersion)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasIndex(s => s.ObjectKey).IsUnique();
        builder.HasIndex(s => new { s.Status, s.ExpiresAtUtc });
        builder.HasIndex(s => new { s.UserId, s.Status });

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

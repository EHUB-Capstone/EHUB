using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(s => s.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(s => s.CheckpointId)
            .HasColumnName("checkpoint_id")
            .IsRequired();

        builder.Property(s => s.SubmittedById)
            .HasColumnName("submitted_by_id");

        builder.Property(s => s.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.SubmittedAt)
            .HasColumnName("submitted_at");

        builder.Property(s => s.VersionNumber)
            .HasColumnName("version_number")
            .IsRequired();

        // Unique Index: ProjectId + CheckpointId + VersionNumber
        builder.HasIndex(s => new { s.ProjectId, s.CheckpointId, s.VersionNumber })
            .IsUnique();

        // Audit & Soft Delete properties configuration
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);

        // Relationships configuration
        builder.HasOne(s => s.Project)
            .WithMany(p => p.Submissions)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Team)
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Checkpoint)
            .WithMany(c => c.Submissions)
            .HasForeignKey(s => s.CheckpointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SubmittedBy)
            .WithMany()
            .HasForeignKey(s => s.SubmittedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

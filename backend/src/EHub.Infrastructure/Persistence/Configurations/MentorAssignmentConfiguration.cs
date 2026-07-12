using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class MentorAssignmentConfiguration : IEntityTypeConfiguration<MentorAssignment>
{
    public void Configure(EntityTypeBuilder<MentorAssignment> builder)
    {
        builder.ToTable("mentor_assignments");

        builder.HasKey(ma => ma.Id);
        builder.Property(ma => ma.Id).HasColumnName("id");

        builder.Property(ma => ma.MentorProfileId)
            .HasColumnName("mentor_profile_id")
            .IsRequired();

        builder.Property(ma => ma.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(ma => ma.ProjectId)
            .HasColumnName("project_id");

        builder.Property(ma => ma.AssignedById)
            .HasColumnName("assigned_by_id")
            .IsRequired();

        builder.Property(ma => ma.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.Property(ma => ma.EndedAt)
            .HasColumnName("ended_at");

        builder.Property(ma => ma.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(ma => ma.Note)
            .HasColumnName("note")
            .HasMaxLength(1000);

        // Indexes
        builder.HasIndex(ma => ma.MentorProfileId);
        builder.HasIndex(ma => ma.TeamId);
        builder.HasIndex(ma => ma.ProjectId);
        builder.HasIndex(ma => ma.AssignedById);
        builder.HasIndex(ma => ma.Status);
        builder.HasIndex(ma => ma.AssignedAt);
        builder.HasIndex(ma => new { ma.MentorProfileId, ma.TeamId, ma.Status });
        builder.HasIndex(ma => new { ma.TeamId, ma.Status });

        // Audit & Soft Delete properties configuration
        builder.Property(ma => ma.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(ma => ma.CreatedBy).HasColumnName("created_by");
        builder.Property(ma => ma.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ma => ma.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ma => ma.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(ma => ma.DeletedAt).HasColumnName("deleted_at");
        builder.Property(ma => ma.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(ma => !ma.IsDeleted);

        // Relationships configuration
        builder.HasOne(ma => ma.MentorProfile)
            .WithMany(mp => mp.Assignments)
            .HasForeignKey(ma => ma.MentorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ma => ma.Team)
            .WithMany(t => t.MentorAssignments)
            .HasForeignKey(ma => ma.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ma => ma.Project)
            .WithMany(p => p.MentorAssignments)
            .HasForeignKey(ma => ma.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ma => ma.AssignedBy)
            .WithMany()
            .HasForeignKey(ma => ma.AssignedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

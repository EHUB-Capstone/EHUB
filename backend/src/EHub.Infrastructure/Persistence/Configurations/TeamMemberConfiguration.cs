using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("team_members");

        builder.HasKey(tm => new { tm.TeamId, tm.StudentId });

        builder.Property(tm => tm.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(tm => tm.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(tm => tm.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(tm => tm.RoleInTeam)
            .HasColumnName("role_in_team")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(tm => tm.CountsTowardActiveTeam)
            .HasColumnName("counts_toward_active_team")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(tm => tm.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        builder.Property(tm => tm.CreatedById)
            .HasColumnName("created_by_id");

        // Unique index: Enforce that a ClassStudent (student in a class) can only be in ONE team in that class
        builder.HasIndex(tm => new { tm.ClassId, tm.StudentId })
            .IsUnique()
            .HasFilter("counts_toward_active_team = true");

        builder.HasIndex(tm => tm.TeamId)
            .IsUnique()
            .HasFilter("role_in_team = 'Leader' AND counts_toward_active_team = true");

        // Relationships configuration
        builder.HasOne(tm => tm.Team)
            .WithMany(t => t.TeamMembers)
            .HasForeignKey(tm => tm.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tm => tm.ClassStudent)
            .WithMany(cs => cs.TeamMembers)
            .HasForeignKey(tm => new { tm.ClassId, tm.StudentId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tm => tm.CreatedBy)
            .WithMany()
            .HasForeignKey(tm => tm.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

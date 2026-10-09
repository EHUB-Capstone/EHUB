using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class TeamFormationConfiguration : IEntityTypeConfiguration<TeamFormation>
{
    public void Configure(EntityTypeBuilder<TeamFormation> builder)
    {
        builder.ToTable("team_formations", table =>
        {
            table.HasCheckConstraint("CK_team_formations_status", "status IN ('Pending', 'Completed', 'Cancelled')");
            table.HasCheckConstraint("CK_team_formations_completed_team", "(status = 'Completed') = (completed_team_id IS NOT NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.ClassId).HasColumnName("class_id").IsRequired();
        builder.Property(item => item.CreatorStudentId).HasColumnName("creator_student_id").IsRequired();
        builder.Property(item => item.ProposedLeaderStudentId).HasColumnName("proposed_leader_student_id").IsRequired();
        builder.Property(item => item.TeamName).HasColumnName("team_name").HasMaxLength(60).IsRequired();
        builder.Property(item => item.NormalizedTeamName).HasColumnName("normalized_team_name").HasMaxLength(60).IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.CompletedTeamId).HasColumnName("completed_team_id");
        builder.Property(item => item.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(item => item.CancelledAtUtc).HasColumnName("cancelled_at_utc");
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasIndex(item => new { item.ClassId, item.NormalizedTeamName })
            .IsUnique().HasFilter("status = 'Pending' AND is_deleted = false");
        builder.HasIndex(item => new { item.ClassId, item.Status });
        builder.HasIndex(item => new { item.CreatorStudentId, item.Status });
        builder.HasIndex(item => item.CompletedTeamId).IsUnique();

        builder.HasOne(item => item.Class).WithMany().HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CreatorStudent).WithMany().HasForeignKey(item => item.CreatorStudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ProposedLeaderStudent).WithMany().HasForeignKey(item => item.ProposedLeaderStudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CompletedTeam).WithMany().HasForeignKey(item => item.CompletedTeamId).OnDelete(DeleteBehavior.Restrict);
    }
}

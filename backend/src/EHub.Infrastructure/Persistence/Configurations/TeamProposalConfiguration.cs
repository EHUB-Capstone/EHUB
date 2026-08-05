using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class TeamProposalConfiguration : IEntityTypeConfiguration<TeamProposal>
{
    public void Configure(EntityTypeBuilder<TeamProposal> builder)
    {
        builder.ToTable("team_proposals");
        builder.HasKey(proposal => proposal.Id);
        builder.Property(proposal => proposal.Id).HasColumnName("id");
        builder.Property(proposal => proposal.ClassId).HasColumnName("class_id").IsRequired();
        builder.Property(proposal => proposal.ProposedByStudentId).HasColumnName("proposed_by_student_id").IsRequired();
        builder.Property(proposal => proposal.TeamName).HasColumnName("team_name").HasMaxLength(100).IsRequired();
        builder.Property(proposal => proposal.Description).HasColumnName("description").HasMaxLength(1_000);
        builder.Property(proposal => proposal.ProjectName).HasColumnName("project_name").HasMaxLength(200);
        builder.Property(proposal => proposal.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(proposal => proposal.SubmittedAtUtc).HasColumnName("submitted_at_utc");
        builder.Property(proposal => proposal.ReviewedAtUtc).HasColumnName("reviewed_at_utc");
        builder.Property(proposal => proposal.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(proposal => proposal.LatestReviewComment).HasColumnName("latest_review_comment").HasMaxLength(1_000);
        builder.Property(proposal => proposal.ApprovedTeamId).HasColumnName("approved_team_id");
        builder.Property(proposal => proposal.Version).IsRowVersion().HasColumnName("xmin");
        builder.Property(proposal => proposal.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(proposal => proposal.CreatedBy).HasColumnName("created_by");
        builder.Property(proposal => proposal.UpdatedAt).HasColumnName("updated_at");
        builder.Property(proposal => proposal.UpdatedBy).HasColumnName("updated_by");
        builder.Property(proposal => proposal.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(proposal => proposal.DeletedAt).HasColumnName("deleted_at");
        builder.Property(proposal => proposal.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(proposal => !proposal.IsDeleted);

        builder.HasIndex(proposal => new { proposal.ClassId, proposal.Status });
        builder.HasIndex(proposal => proposal.ProposedByStudentId);
        builder.HasIndex(proposal => proposal.ApprovedTeamId).IsUnique();
        builder.HasOne(proposal => proposal.Class).WithMany().HasForeignKey(proposal => proposal.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(proposal => proposal.ProposedByStudent).WithMany().HasForeignKey(proposal => proposal.ProposedByStudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(proposal => proposal.ReviewedByUser).WithMany().HasForeignKey(proposal => proposal.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(proposal => proposal.ApprovedTeam).WithMany(team => team.ApprovedProposals).HasForeignKey(proposal => proposal.ApprovedTeamId).OnDelete(DeleteBehavior.Restrict);
    }
}

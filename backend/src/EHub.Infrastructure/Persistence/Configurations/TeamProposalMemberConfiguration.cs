using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class TeamProposalMemberConfiguration : IEntityTypeConfiguration<TeamProposalMember>
{
    public void Configure(EntityTypeBuilder<TeamProposalMember> builder)
    {
        builder.ToTable("team_proposal_members");
        builder.HasKey(member => new { member.ProposalId, member.StudentId });
        builder.Property(member => member.ProposalId).HasColumnName("proposal_id");
        builder.Property(member => member.ClassId).HasColumnName("class_id");
        builder.Property(member => member.StudentId).HasColumnName("student_id");
        builder.Property(member => member.IsLeader).HasColumnName("is_leader").IsRequired();
        builder.Property(member => member.IsIncluded).HasColumnName("is_included").HasDefaultValue(true).IsRequired();
        builder.Property(member => member.CountsTowardOpenProposal).HasColumnName("counts_toward_open_proposal").HasDefaultValue(true).IsRequired();
        builder.HasIndex(member => new { member.ClassId, member.StudentId })
            .IsUnique()
            .HasFilter("counts_toward_open_proposal = true");
        builder.HasIndex(member => member.ProposalId)
            .IsUnique()
            .HasFilter("is_leader = true");
        builder.HasOne(member => member.Proposal).WithMany(proposal => proposal.Members).HasForeignKey(member => member.ProposalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(member => member.ClassStudent).WithMany().HasForeignKey(member => new { member.ClassId, member.StudentId }).OnDelete(DeleteBehavior.Restrict);
    }
}

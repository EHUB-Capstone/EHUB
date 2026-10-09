using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class TeamProposalHistoryConfiguration : IEntityTypeConfiguration<TeamProposalHistory>
{
    public void Configure(EntityTypeBuilder<TeamProposalHistory> builder)
    {
        builder.ToTable("team_proposal_history");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.ProposalId).HasColumnName("proposal_id");
        builder.Property(item => item.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(item => item.Comment).HasColumnName("comment").HasMaxLength(1_000);
        builder.Property(item => item.PerformedByUserId).HasColumnName("performed_by_user_id");
        builder.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(item => item.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
        builder.HasIndex(item => new { item.ProposalId, item.OccurredAtUtc });
        builder.HasOne(item => item.Proposal).WithMany(proposal => proposal.History).HasForeignKey(item => item.ProposalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.PerformedByUser).WithMany().HasForeignKey(item => item.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

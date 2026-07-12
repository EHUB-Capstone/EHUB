using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ProjectProposalVersionConfiguration : IEntityTypeConfiguration<ProjectProposalVersion>
{
    public void Configure(EntityTypeBuilder<ProjectProposalVersion> builder)
    {
        builder.ToTable("project_proposal_versions");

        builder.HasKey(pv => pv.Id);
        builder.Property(pv => pv.Id).HasColumnName("id");

        builder.Property(pv => pv.ProjectProposalId)
            .HasColumnName("project_proposal_id")
            .IsRequired();

        builder.Property(pv => pv.VersionNumber)
            .HasColumnName("version_number")
            .IsRequired();

        builder.Property(pv => pv.SnapshotJson)
            .HasColumnName("snapshot_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(pv => pv.ChangeNote)
            .HasColumnName("change_note")
            .HasMaxLength(1000);

        builder.Property(pv => pv.ChangedById)
            .HasColumnName("changed_by_id")
            .IsRequired();

        builder.Property(pv => pv.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(pv => new { pv.ProjectProposalId, pv.VersionNumber })
            .IsUnique();

        builder.HasIndex(pv => pv.ChangedById);
        builder.HasIndex(pv => pv.CreatedAt);

        // Relationships configuration
        builder.HasOne(pv => pv.ProjectProposal)
            .WithMany(pp => pp.Versions)
            .HasForeignKey(pv => pv.ProjectProposalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pv => pv.ChangedBy)
            .WithMany()
            .HasForeignKey(pv => pv.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

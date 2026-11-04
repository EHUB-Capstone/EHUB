using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ProjectCompetitionEntryConfiguration : IEntityTypeConfiguration<ProjectCompetitionEntry>
{
    public void Configure(EntityTypeBuilder<ProjectCompetitionEntry> builder)
    {
        builder.ToTable("project_competition_entries");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasColumnName("id");

        builder.Property(entry => entry.ProjectId).HasColumnName("project_id").IsRequired();

        builder.Property(entry => entry.Competition)
            .HasColumnName("competition")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(entry => entry.OtherName)
            .HasColumnName("other_name")
            .HasMaxLength(100);

        builder.Property(entry => entry.NormalizedOtherName)
            .HasColumnName("normalized_other_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entry => entry.Year).HasColumnName("year").IsRequired();

        builder.Property(entry => entry.Stage)
            .HasColumnName("stage")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entry => entry.CreatedById).HasColumnName("created_by_id");
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entry => entry.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(entry => entry.UpdatedAt).HasColumnName("updated_at");

        // A project enters a given competition once a year; "Others" is told apart by its name.
        builder.HasIndex(entry => new { entry.ProjectId, entry.Competition, entry.NormalizedOtherName, entry.Year })
            .IsUnique();

        // Filters and the export read by competition, year and stage.
        builder.HasIndex(entry => new { entry.Competition, entry.Year, entry.Stage });

        builder.HasOne(entry => entry.Project)
            .WithMany(project => project.ProjectCompetitionEntries)
            .HasForeignKey(entry => entry.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
